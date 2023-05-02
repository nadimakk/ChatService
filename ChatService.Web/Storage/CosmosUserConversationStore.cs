using System.Net;
using System.Reflection.PortableExecutable;
using ChatService.Web.Dtos;
using ChatService.Web.Enums;
using ChatService.Web.Exceptions;
using ChatService.Web.Storage.Entities;
using ChatService.Web.Utilities;
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

    public async Task UpsertUserConversation(UserConversation userConversation)
    {
        ValidateUserConversation(userConversation);

        try
        {
            await Container.UpsertItemAsync(ToEntity(userConversation), new PartitionKey(userConversation.Username));
        }
        catch (CosmosException e)
        {
            ServiceAvailabilityCheckerUtilities.ThrowIfCosmosUnavailable(e);
            throw;
        }
    }

    public async Task<UserConversation?> GetUserConversation(string username, string conversationId)
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
                return null;
            }
            ServiceAvailabilityCheckerUtilities.ThrowIfCosmosUnavailable(e);
            throw;
        }
    }

    public async Task<GetUserConversationsResult> GetUserConversations(GetConversationsParameters parameters)
    {
        ValidateUsername(parameters.Username);
        ValidateLimit(parameters.Limit);
        ValidateLastSeenMessageTime(parameters.LastSeenConversationTime);
        
        List<UserConversation> userConversations = new();
        string? nextContinuationToken = null;
        
        QueryRequestOptions options = new();
        options.MaxItemCount = parameters.Limit;

        try
        {
            IQueryable<UserConversationEntity> query = Container
                .GetItemLinqQueryable<UserConversationEntity>(
                    allowSynchronousQueryExecution: false, parameters.ContinuationToken, options)
                .Where(e => e.partitionKey == parameters.Username && e.LastModifiedTime > parameters.LastSeenConversationTime);
            
            if (parameters.Order == OrderBy.ASC)
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

            return new GetUserConversationsResult
            {
                UserConversations = userConversations,
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
            ServiceAvailabilityCheckerUtilities.ThrowIfCosmosUnavailable(e);
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
        if (string.IsNullOrWhiteSpace(conversationId))
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