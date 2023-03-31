using ChatService.Web.Dtos;
using ChatService.Web.Enums;
using ChatService.Web.Exceptions;
using ChatService.Web.Storage;
using ChatService.Web.Utilities;

namespace ChatService.Web.Services;

public class UserConversationService : IUserConversationService
{

    private readonly IMessageService _messageService;
    private readonly IUserConversationStore _userConversationStore;
    private readonly IProfileService _profileService;

    public UserConversationService(IMessageService messageService, IUserConversationStore userConversationStore,
        IProfileService profileService)
    {
        _messageService = messageService;
        _userConversationStore = userConversationStore;
        _profileService = profileService;
    }

    public async Task<StartConversationServiceResult> CreateConversation(StartConversationRequest request)
    {
        ValidateStartConversationRequest(request);

        string username1 = request.Participants.ElementAt(0);
        string username2 = request.Participants.ElementAt(1);

        if (!await _profileService.ProfileExists(username1))
        {
            throw new ProfileNotFoundException($"A profile with the username {username1} was not found.");
        }

        if (!await _profileService.ProfileExists(username2))
        {
            throw new ProfileNotFoundException($"A profile with the username {username2} was not found.");
        }

        string conversationId = ConversationIdUtilities.GenerateConversationId(username1, username2);

        long unixTimeNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        SendMessageRequest sendMessageRequest = new SendMessageRequest
        {
            MessageId = request.FirstMessage.MessageId,
            SenderUsername = request.FirstMessage.SenderUsername,
            Text = request.FirstMessage.Text
        };
        await _messageService.AddFirstMessage(conversationId, sendMessageRequest);

        UserConversation userConversation1 = new UserConversation
        {
            Username = username1,
            ConversationId = conversationId,
            LastModifiedTime = unixTimeNow
        };
        await _userConversationStore.CreateUserConversation(userConversation1);

        UserConversation userConversation2 = new UserConversation
        {
            Username = username2,
            ConversationId = conversationId,
            LastModifiedTime = unixTimeNow
        };
        await _userConversationStore.CreateUserConversation(userConversation2);

        return new StartConversationServiceResult
        {
            ConversationId = conversationId,
            CreatedUnixTime = unixTimeNow
        };
    }

    public async Task<GetUserConversationsServiceResult> GetUserConversations(
        string username, int limit, OrderBy orderBy, string? continuationToken, long lastSeenConversationTime)
    {
        if (string.IsNullOrWhiteSpace(username))
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

        if (!await _profileService.ProfileExists(username))
        {
            throw new UserNotFoundException($"User {username} was not found.");
        }

        var result = await _userConversationStore.GetUserConversations(
            username, limit, orderBy, continuationToken, lastSeenConversationTime);

        List<Conversation> conversations = await UserConversationsToConversations(result.UserConversations);

        return new GetUserConversationsServiceResult
        {
            Conversations = conversations,
            NextContinuationToken = result.NextContinuationToken
        };
    }

    private async Task<List<Conversation>> UserConversationsToConversations(List<UserConversation> userConversations)
    {
        List<Conversation> conversations = new();

        foreach (UserConversation userConversation in userConversations)
        {
            string[] usernames = userConversation.ConversationId.Split('_');
            string recipientUsername;

            if (usernames[0].Equals(userConversation.Username))
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
                ConversationId = userConversation.ConversationId,
                LastModifiedUnixTime = userConversation.LastModifiedTime,
                Recipient = recipientProfile
            };

            conversations.Add(conversation);
        }

        return conversations;
    }

    private void ValidateStartConversationRequest(StartConversationRequest request)
    {
        if (request == null)
        {
            throw new ArgumentException($"StartConversationRequest is null.");
        }

        if (request.Participants.Count < 2 ||
            string.IsNullOrWhiteSpace(request.Participants.ElementAt(0)) ||
            string.IsNullOrWhiteSpace(request.Participants.ElementAt(1)) ||
            request.Participants.ElementAt(0).Equals(request.Participants.ElementAt(1)))
        {
            throw new ArgumentException(
                $"Invalid participants list ${request.Participants}. There must be 2 unique participant usernames");
        }

        if (string.IsNullOrWhiteSpace(request.FirstMessage.MessageId) ||
            string.IsNullOrWhiteSpace(request.FirstMessage.SenderUsername) ||
            string.IsNullOrWhiteSpace(request.FirstMessage.Text))
        {
            throw new ArgumentException($"Invalid FirstMessage {request.FirstMessage}.");
        }
    }
}