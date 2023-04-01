using System.Net;
using ChatService.Web.Dtos;
using ChatService.Web.Enums;
using ChatService.Web.Exceptions;
using ChatService.Web.Storage.Entities;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;

namespace ChatService.Web.Storage;

public class CosmosUserConversationStore : IUserConversationStore
{
    private readonly CosmosClient _cosmosClient;

    public CosmosUserConversationStore(CosmosClient cosmosClient)
    {
        _cosmosClient = cosmosClient;
    }
    
    private Container Container => _cosmosClient.GetDatabase("chatService").GetContainer("sharedContainer");

    public async Task CreateUserConversation(UserConversation userConversation)
    {
        ValidateUserConversation(userConversation);

        try
        {
            await Container.CreateItemAsync(ToEntity(userConversation), new PartitionKey(userConversation.Username));
        }
        catch (CosmosException e)
        {
            if (e.StatusCode == HttpStatusCode.Conflict)
            {
                throw new UserConversationExistsException($"A user conversation with conversation ID {userConversation.ConversationId} already exists.");
            }
            throw;
        }
    }

    public async Task<UserConversation> GetUserConversation(string username, string conversationId)
    {
        ValidateUsername(username);
        ValidateConversationId(conversationId);

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
        ValidateUsername(username);
        ValidateLimit(limit);
        ValidateLastSeenMessageTime(lastSeenConversationTime);
        
        List<UserConversation> userConversations = new ();
        string? nextContinuationToken = null;
        
        QueryRequestOptions options = new QueryRequestOptions();
        options.MaxItemCount = limit;

        try
        {
            IQueryable<UserConversationEntity> query = Container
                .GetItemLinqQueryable<UserConversationEntity>(false, continuationToken, options)
                .Where(e => e.partitionKey == username && e.LastModifiedTime > lastSeenConversationTime);
            
            if (order == OrderBy.ASC)
            {
                query = query.OrderBy(e => e.LastModifiedTime);
            }
            else
            {
                query = query.OrderByDescending(e => e.LastModifiedTime);
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
        catch (CosmosException e)
        {
            if (e.StatusCode == HttpStatusCode.BadRequest)
            {
                throw new InvalidContinuationTokenException($"Continuation token {continuationToken} is invalid.");
            }
            throw;
        }
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
            partitionKey: userConversation.Username,
            id: userConversation.ConversationId,
            userConversation.LastModifiedTime
        );
    }

    private static UserConversation ToUserConversation(UserConversationEntity entity)
    {
        return new UserConversation {
            Username = entity.partitionKey,
            ConversationId = entity.id,
            LastModifiedTime = entity.LastModifiedTime
        };
    }

    private void ValidateUserConversation(UserConversation userConversation)
    {
        if (userConversation == null ||
            string.IsNullOrWhiteSpace(userConversation.Username) ||
            string.IsNullOrWhiteSpace(userConversation.ConversationId) ||
            userConversation.LastModifiedTime < 0
           )
        {
            throw new ArgumentException($"Invalid user conversation {userConversation}", nameof(userConversation));
        }
    }
    
    private void ValidateUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException($"Invalid username {username}");
        }
    }
    
    private void ValidateConversationId(string conversationId)
    {
        if (string.IsNullOrWhiteSpace(conversationId) || !conversationId.Contains('_'))
        {
            throw new ArgumentException($"Invalid conversationId {conversationId}.");
        }
    }
    
    private void ValidateLimit(int limit)
    {
        if (limit <= 0)
        {
            throw new ArgumentException($"Invalid limit {limit}. Limit must be greater or equal to 1.");
        }
    }
    
    private void ValidateLastSeenMessageTime(long lastSeenConversationTime)
    {
        if (lastSeenConversationTime < 0)
        {
            throw new ArgumentException(
                $"Invalid lastSeenConversationTime {lastSeenConversationTime}. LastSeenConversationTime must be greater or equal to 0.");
        }
    }
}