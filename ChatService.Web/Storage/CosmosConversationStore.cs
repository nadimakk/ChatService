using System.Net;
using ChatService.Web.Dtos;
using ChatService.Web.Enums;
using ChatService.Web.Exceptions;
using ChatService.Web.Storage.Entities;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;

namespace ChatService.Web.Storage;

public class CosmosConversationStore : IConversationStore
{
    private readonly CosmosClient _cosmosClient;

    public CosmosConversationStore(CosmosClient cosmosClient)
    {
        _cosmosClient = cosmosClient;
    }
    
    private Container Container => _cosmosClient.GetDatabase("chatService").GetContainer("sharedContainer");

    public async Task CreateUserConversation(UserConversation userConversation)
    {
        if (userConversation == null ||
            string.IsNullOrWhiteSpace(userConversation.username) ||
            string.IsNullOrWhiteSpace(userConversation.conversationId) ||
            userConversation.lastModifiedTime < 0
           )
        {
            throw new ArgumentException($"Invalid user conversation {userConversation}", nameof(userConversation));
        }

        try
        {
            await Container.CreateItemAsync(ToEntity(userConversation), new PartitionKey(userConversation.username));
        }
        catch (CosmosException e)
        {
            if (e.StatusCode == HttpStatusCode.Conflict)
            {
                throw new UserConversationExistsException($"A user conversation with conversation ID {userConversation.conversationId} already exists.");
            }
            throw;
        }
    }

    public async Task<UserConversation> GetUserConversation(string username, string conversationId)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException($"Invalid username {username}");
        }

        if (string.IsNullOrWhiteSpace(conversationId))
        {
            throw new ArgumentException($"Invalid conversationId {conversationId}");
        }
        try
        {
            var entity = await Container.ReadItemAsync<UserConversationEntity>(
                id: conversationId,
                partitionKey: new PartitionKey(username),
                new ItemRequestOptions
                {
                    ConsistencyLevel = ConsistencyLevel.Session
                }
            );
            return ToUserConversation(entity);
        }
        catch (CosmosException e)
        {
            if (e.StatusCode == HttpStatusCode.NotFound)
            {
                throw new UserConversationNotFoundException($"A UserConversation with conversationId {conversationId} was not found.");
            }
            throw;
        }
    }

    public async Task<(List<UserConversation> UserConversations, string NextContinuationToken)> GetUserConversations
        (string username, int limit, OrderBy order, string? continuationToken, long lastSeenConversationTime)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("Username cannot be null or empty.");
        }
        if (limit <= 0)
        {
            throw new ArgumentException($"Invalid limit {limit}");
        }

        if (lastSeenConversationTime < 0)
        {
            throw new ArgumentException($"Invalid lastSeenConversationTime {lastSeenConversationTime}");
        }
        
        List<UserConversation> userConversations = new ();
        string? nextContinuationToken = null;
        
        QueryRequestOptions options = new QueryRequestOptions();
        options.MaxItemCount = limit;

        IQueryable<UserConversationEntity> query = Container
            .GetItemLinqQueryable<UserConversationEntity>(false, continuationToken, options)
            .Where(e => e.partitionKey == username && e.lastModifiedTime > lastSeenConversationTime);
        
        if (order == OrderBy.ASC)
        {
            query = query.OrderBy(e => e.lastModifiedTime);
        }
        else
        {
            query = query.OrderByDescending(e => e.lastModifiedTime);
        }
        
        using (FeedIterator<UserConversationEntity> iterator = query.ToFeedIterator())
        {
            FeedResponse<UserConversationEntity> response = await iterator.ReadNextAsync();
            var receivedUserConversations = response.Select(ToUserConversation);
            
            userConversations.AddRange(receivedUserConversations);
            
            nextContinuationToken = response.ContinuationToken;
        };

        return (userConversations, nextContinuationToken);
    }

    public async Task DeleteUserConversation(string username, string conversationId)
    {
        try
        {
            await Container.DeleteItemAsync<UserConversation>(
                id: conversationId, 
                partitionKey: new PartitionKey(username));
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

    private static UserConversationEntity ToEntity(UserConversation userConversation)
    {
        return new UserConversationEntity(
            partitionKey: userConversation.username,
            id: userConversation.conversationId,
            userConversation.lastModifiedTime
        );
    }

    private static UserConversation ToUserConversation(UserConversationEntity entity)
    {
        return new UserConversation {
            username = entity.partitionKey,
            conversationId = entity.id,
            lastModifiedTime = entity.lastModifiedTime
        };
    }
}