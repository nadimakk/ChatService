using System.Net;
using ChatService.Web.Dtos;
using ChatService.Web.Enums;
using ChatService.Web.Exceptions;
using ChatService.Web.Storage.Entities;
using ChatService.Web.Utilities;
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
        ValidateMessage(message);
        
        try
        {
            await Container.CreateItemAsync(ToEntity(conversationId, message), new PartitionKey(conversationId));
        }
        catch (CosmosException e)
        {
            if (e.StatusCode == HttpStatusCode.Conflict)
            {
                throw new MessageExistsException($"A message with ID {message.MessageId} already exists.");
            }
            ServiceAvailabilityCheckerUtilities.ThrowIfCosmosUnavailable(e);
            throw;
        }
    }

    public async Task<Message?> GetMessage(string conversationId, string messageId)
    {
        ValidateConversationId(conversationId);
        ValidateMessageId(messageId);

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
                return null;
            }
            ServiceAvailabilityCheckerUtilities.ThrowIfCosmosUnavailable(e);
            throw;
        }
    }

    public async Task<GetMessagesResult> GetMessages(string conversationId, GetMessagesParameters parameters)
    {
        ValidateConversationId(conversationId);
        ValidateLimit(parameters.Limit);
        ValidateLastSeenMessageTime(parameters.LastSeenMessageTime);

        List<Message> messages = new();
        string? nextContinuationToken = null;
        
        QueryRequestOptions options = new();
        options.MaxItemCount = parameters.Limit;

        try
        {
            IQueryable<MessageEntity> query = Container
                .GetItemLinqQueryable<MessageEntity>(
                    allowSynchronousQueryExecution: false, parameters.ContinuationToken, options)
                .Where(e => e.partitionKey == conversationId && e.UnixTime > parameters.LastSeenMessageTime);
        
            if (parameters.Order == OrderBy.ASC)
            {
                query = query.OrderBy(e => e.UnixTime);
            }
            else
            {
                query = query.OrderByDescending(e => e.UnixTime);
            }
        
            using (FeedIterator<MessageEntity> iterator = query.ToFeedIterator())
            {
                FeedResponse<MessageEntity> response = await iterator.ReadNextAsync();
                var receivedMessages = response.Select(ToMessage);
            
                messages.AddRange(receivedMessages);
            
                nextContinuationToken = response.ContinuationToken;
            };

            return new GetMessagesResult
            {
                Messages = messages,
                NextContinuationToken = nextContinuationToken
            };
        }
        catch (CosmosException e)
        {
            if (e.StatusCode == HttpStatusCode.BadRequest)
            {
                throw new InvalidContinuationTokenException($"Continuation token {parameters.ContinuationToken} is invalid.");
            }
            ServiceAvailabilityCheckerUtilities.ThrowIfCosmosUnavailable(e);
            throw;
        }
    }

    public async Task<bool> ConversationPartitionExists(string conversationId)
    {
        GetMessagesParameters parameters = new()
        {
            Limit = 1,
            Order = OrderBy.ASC,
            ContinuationToken = null,
            LastSeenMessageTime = 0
        };
        GetMessagesResult result = await GetMessages(conversationId, parameters);
        return (result.Messages.Count > 0);
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
            ServiceAvailabilityCheckerUtilities.ThrowIfCosmosUnavailable(e);
            throw;
        }
    }

    private static MessageEntity ToEntity(string conversationId, Message message)
    {
        return new MessageEntity(
            partitionKey: conversationId,
            id: message.MessageId,
            message.UnixTime,
            message.SenderUsername,
            message.Text
        );
    }

    private static Message ToMessage(MessageEntity entity)
    {
        return new Message
        {
            MessageId = entity.id,
            UnixTime = entity.UnixTime,
            SenderUsername = entity.SenderUsername,
            Text = entity.Text
        };
    }

    private void ValidateMessage(Message message)
    {
        if (message == null ||
            string.IsNullOrWhiteSpace(message.MessageId) ||
            string.IsNullOrWhiteSpace(message.SenderUsername) ||
            string.IsNullOrWhiteSpace(message.Text) ||
            message.UnixTime < 0
           )
        {
            throw new ArgumentException($"Invalid message {message}", nameof(message));
        }
    }
    
    private void ValidateConversationId(string conversationId)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
        {
            throw new ArgumentException($"Invalid conversationId {conversationId}.");
        }
    }

    private void ValidateMessageId(string messageId)
    {
        if (string.IsNullOrWhiteSpace(messageId))
        {
            throw new ArgumentException($"Invalid messageId {messageId}");
        }
    }
    
    private void ValidateLimit(int limit)
    {
        if (limit <= 0)
        {
            throw new ArgumentException($"Invalid limit {limit}. Limit must be greater or equal to 1.");
        }
    }
    
    private void ValidateLastSeenMessageTime(long lastSeenMessageTime)
    {
        if (lastSeenMessageTime < 0)
        {
            throw new ArgumentException(
                $"Invalid lastSeenMessageTime {lastSeenMessageTime}. LastSeenMessageTime must be greater or equal to 0.");
        }
    }
}