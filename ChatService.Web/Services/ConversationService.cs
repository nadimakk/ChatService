using ChatService.Web.Dtos;
using ChatService.Web.Enums;
using ChatService.Web.Exceptions;
using ChatService.Web.Storage;

namespace ChatService.Web.Services;

public class ConversationService : IConversationService
{

    private readonly IMessageStore _messageStore;
    private readonly IConversationStore _conversationStore;
    private readonly IProfileService _profileService;

    public ConversationService(IMessageStore messageStore, IConversationStore conversationStore, IProfileService profileService)
    {
        _messageStore = messageStore;
        _conversationStore = conversationStore;
        _profileService = profileService;
    }

    public async Task<StartConversationResponse> CreateConversation(StartConversationRequest request)
    {
        if (request == null)
        {
            throw new ArgumentException($"StartConversationRequest is null.");
        }
        
        if (request.participants.Count < 2 ||
            //TODO:
            string.IsNullOrEmpty(request.participants.ElementAt(0)) || //WRITE A TEST TO SEE THE PYTHON THING!!!!!!!!!!!!!!
            string.IsNullOrEmpty(request.participants.ElementAt(1)) ||
            request.participants.ElementAt(0).Equals(request.participants.ElementAt(1)))
        {
            throw new ArgumentException(
                $"Invalid participants list ${request.participants}. There must be 2 unique participant usernames");
        }
        
        if (string.IsNullOrEmpty(request.firstMessage.id) ||
            string.IsNullOrEmpty(request.firstMessage.SenderUsername) ||
            string.IsNullOrEmpty(request.firstMessage.text))
        {
            throw new ArgumentException($"Invalid FirstMessage {request.firstMessage}.");
        }

        string conversationId;
        string username1 = request.participants.ElementAt(0);
        string username2 = request.participants.ElementAt(1);
        
        if (!await _profileService.ProfileExists(username1))
        {
            throw new ProfileNotFoundException($"A profile with the username {username1} was not found.");
        }
        if (!await _profileService.ProfileExists(username2))
        {
            throw new ProfileNotFoundException($"A profile with the username {username2} was not found.");
        }

        if (username1.CompareTo(username2) < 0)
        {
            conversationId = username1 + "_" + username2;
        }
        else
        {
            conversationId = username2 + "_" + username1;
        }

        long unixTimeNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        
        //TODO:
        ////////////// MOVE THIS TO MESSAGE SERVICE and call it from here
        Message message = new Message
        {
            id = request.firstMessage.id,
            unixTime = unixTimeNow,
            senderUsername = request.firstMessage.SenderUsername,
            text = request.firstMessage.text
        };
        await _messageStore.AddMessage(conversationId, message);
        //////////////////////////////////////////////////////////
        
        UserConversation userConversation1 = new UserConversation
        {
            username = username1,
            conversationId = conversationId,
            lastModifiedTime = unixTimeNow
        };
        await _conversationStore.CreateUserConversation(userConversation1);
        
        UserConversation userConversation2 = new UserConversation
        {
            username = username2,
            conversationId = conversationId,
            lastModifiedTime = unixTimeNow
        };
        await _conversationStore.CreateUserConversation(userConversation2);

        return new StartConversationResponse
        {
            Id = conversationId,
            CreatedUnixTime = unixTimeNow
        };
    }

    public async Task<GetConversationsResponse> GetConversations(
        string username, int limit, OrderBy orderBy, string? continuationToken, long lastSeenConversationTime)
    {
        if (string.IsNullOrEmpty(username))
        {
            throw new ArgumentException($"Invalid username {username}.");
        }

        if (limit <= 0)
        {
            throw new ArgumentException($"Invalid limit {limit}. Limit must be greater or equal to 1.");
        }

        if (lastSeenConversationTime < 0)
        {
            throw new ArgumentException(
                $"Invalid lastSeenConversationTime {lastSeenConversationTime}. lastSeenConversationTime must be greater or equal to 0.");
        }

        var result = await _conversationStore.GetUserConversations(
            username, limit, orderBy, continuationToken, lastSeenConversationTime);

        List<Conversation> conversations = await UserConversationsToConversations(result.UserConversations);
        
        return new GetConversationsResponse
        {
            conversations = conversations,
            nextContinuationToken = result.NextContinuationToken
        };
    }

    private async Task<List<Conversation>> UserConversationsToConversations(List<UserConversation> userConversations)
    {
        List<Conversation> conversations = new();
        
        foreach (UserConversation userConversation in userConversations)
        {
            string[] usernames = userConversation.conversationId.Split('_');
            string recipientUsername;

            if (usernames[0].Equals(userConversation.username))
            {
                recipientUsername = usernames[1];
            }
            else
            {
                recipientUsername = usernames[0];
            }

            Profile recipientProfile = await _profileService.GetProfile(recipientUsername);

            Conversation conversation = new Conversation
            {
                id = userConversation.conversationId,
                lastModifiedUnixTime = userConversation.lastModifiedTime,
                recipient = recipientProfile
            };
            
            conversations.Add(conversation);
        }

        return conversations;
    }
}