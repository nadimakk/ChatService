using System.Net;
using ChatService.Web.Dtos;
using ChatService.Web.Exceptions;
using ChatService.Web.Storage.Entities;
using Microsoft.Azure.Cosmos;

namespace ChatService.Web.Storage;

public class CosmosConversationStore : IConversationStore
{
    private readonly CosmosClient _cosmosClient;

    public CosmosConversationStore(CosmosClient cosmosClient)
    {
        _cosmosClient = cosmosClient;
    }
    
    private Container Container => _cosmosClient.GetDatabase("chatService").GetContainer("sharedContainer");

    
    public async Task CreateConversation(Conversation conversation)
    {
        if (conversation == null ||
            conversation.participants.Count < 2 ||
            string.IsNullOrWhiteSpace(conversation.id)
            )
        {
            throw new ArgumentException($"Invalid conversation {conversation}", nameof(conversation));
        }

        try
        {
            await Container.CreateItemAsync(ToEntity(conversation), new PartitionKey(conversation.id));
        }
        catch (CosmosException e)
        {
            if (e.StatusCode == HttpStatusCode.Conflict)
            {
                throw new ConversationExistsException($"A conversation with id {conversation.id} already exists.");
            }
            throw;
        }
    }

    public async Task CreateUserConversation(UserConversation userConversation)
    {
        if (userConversation == null ||
            string.IsNullOrWhiteSpace(userConversation.username) ||
            string.IsNullOrWhiteSpace(userConversation.conversationId)
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
    
    public async Task DeleteConversation(string conversationId)
    {
        try
        {
            await Container.DeleteItemAsync<Conversation>(
                id: conversationId, 
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
    
    private static ConversationEntity ToEntity(Conversation conversation)
    {
        return new ConversationEntity(
            partitionKey: conversation.id,
            id: conversation.id,
            conversation.lastModifiedTime,
            conversation.participants
        );
    }

    private static Conversation ToConversation(ConversationEntity entity)
    {
        return new Conversation(
            id: entity.id,
            entity.lastModifiedTime,
            entity.participants
        );
    }
    
    private static UserConversationEntity ToEntity(UserConversation userConversation)
    {
        return new UserConversationEntity(
            partitionKey: userConversation.username,
            id: userConversation.conversationId
        );
    }

    private static UserConversation ToUserConversation(UserConversationEntity entity)
    {
        return new UserConversation(
            username: entity.partitionKey,
            conversationId: entity.id
        );
    }
}