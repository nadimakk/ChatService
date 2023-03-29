using System.Net;
using ChatService.Web.Dtos;
using ChatService.Web.Enums;
using ChatService.Web.Exceptions;
using ChatService.Web.Storage.Entities;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;

namespace ChatService.Web.Storage;

public class CosmosMessageStore : IMessageStore
{
    private readonly CosmosClient _cosmosClient;

    public CosmosMessageStore(CosmosClient cosmosClient)
    {
        _cosmosClient = cosmosClient;
    }
    
    private Container Container => _cosmosClient.GetDatabase("chatService").GetContainer("sharedContainer");
    
    public async Task AddMessage(string conversationId, Message message)
    {
        if (message == null ||
            string.IsNullOrWhiteSpace(message.id) ||
            string.IsNullOrWhiteSpace(message.senderUsername) ||
            string.IsNullOrWhiteSpace(message.text)
           )
        {
            throw new ArgumentException($"Invalid message {message}", nameof(message));
        }
        
        try
        {
            await Container.CreateItemAsync(ToEntity(conversationId, message), new PartitionKey(conversationId));
        }
        catch (CosmosException e)
        {
            if (e.StatusCode == HttpStatusCode.Conflict)
            {
                throw new MessageExistsException($"A message with ID {message.id} already exists.");
            }
            throw;
        }
    }

    public async Task<Message> GetMessage(string conversationId, string messageId)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
        {
            throw new ArgumentException($"Invalid conversationId {conversationId}");
        }
        if (string.IsNullOrWhiteSpace(messageId))
        {
            throw new ArgumentException($"Invalid messageId {messageId}");
        }
        
        try
        {
            var entity = await Container.ReadItemAsync<MessageEntity>(
                id: messageId,
                partitionKey: new PartitionKey(conversationId),
                new ItemRequestOptions
                {
                    ConsistencyLevel = ConsistencyLevel.Session
                }
            );
            return ToMessage(entity);
        }
        catch (CosmosException e)
        {
            if (e.StatusCode == HttpStatusCode.NotFound)
            {
                throw new MessageNotFoundException($"A message with messageId {messageId} was not found.");
            }
            throw;
        }
    }

    public async Task<(List<Message> Messages, string NextContinuationToken)> GetMessages(
        string conversationId, int limit, OrderBy order, string? continuationToken, long lastSeenMessageTime)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
        {
            throw new ArgumentException("ConversationId cannot be null or empty.");
        }
        if (limit <= 0)
        {
            throw new ArgumentException($"Invalid limit {limit}");
        }
        
        List<Message> messages = new ();
        string nextContinuationToken = null;
        
        QueryRequestOptions options = new QueryRequestOptions();
        options.MaxItemCount = limit;

        IQueryable<MessageEntity> query = Container
            .GetItemLinqQueryable<MessageEntity>(false, continuationToken, options)
            .Where(e => e.partitionKey == conversationId && e.unixTime > lastSeenMessageTime);
        
        if (order == OrderBy.ASC)
        {
            query = query.OrderBy(e => e.unixTime);
        }
        else
        {
            query = query.OrderByDescending(e => e.unixTime);
        }
        
        using (FeedIterator<MessageEntity> iterator = query.ToFeedIterator())
        {
            FeedResponse<MessageEntity> response = await iterator.ReadNextAsync();
            var receivedUserConversations = response.Select(ToMessage);
            
            messages.AddRange(receivedUserConversations);
            
            nextContinuationToken = response.ContinuationToken;
        };

        return (messages, nextContinuationToken);
    }

    public async Task DeleteMessage(string conversationId, string messageId)
    {
        try
        {
            await Container.DeleteItemAsync<Message>(
                id: messageId, 
                partitionKey: new PartitionKey(conversationId));
        }
        catch (CosmosException e)
        {
            if (e.StatusCode == HttpStatusCode.NotFound)
            {
                return;
            }
            throw;
        }
    }

    private static MessageEntity ToEntity(string conversationId, Message message)
    {
        return new MessageEntity(
            partitionKey: conversationId,
            id: message.id,
            message.unixTime,
            message.senderUsername,
            message.text
        );
    }

    private static Message ToMessage(MessageEntity entity)
    {
        return new Message
        {
            id = entity.id,
            unixTime = entity.unixTime,
            senderUsername = entity.senderUsername,
            text = entity.text
        };
    }
}