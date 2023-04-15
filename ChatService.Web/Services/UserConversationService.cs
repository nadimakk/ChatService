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

    public async Task<StartConversationResult> CreateConversation(StartConversationRequest request)
    {
        ValidateStartConversationRequest(request);
        await EnsureThatParticipantsExist(request.Participants);
        
        string username1 = request.Participants.ElementAt(0);
        string username2 = request.Participants.ElementAt(1);
        string conversationId = ConversationIdUtilities.GenerateConversationId(username1, username2);

        SendMessageRequest sendMessageRequest = new()
        {
            MessageId = request.FirstMessage.MessageId,
            SenderUsername = request.FirstMessage.SenderUsername,
            Text = request.FirstMessage.Text
        };
        await _messageService.AddFirstMessage(conversationId, sendMessageRequest);
        
        long unixTimeNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        
        await Task.WhenAll(
            CreateUserConversation(username1, conversationId, unixTimeNow),
            CreateUserConversation(username2, conversationId, unixTimeNow)
        );
        
        return new StartConversationResult
        {
            ConversationId = conversationId,
            CreatedUnixTime = unixTimeNow
        };
    }
    
    public async Task<GetConversationsResult> GetUserConversations(
        string username, GetUserConversationsParameters parameters)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException($"Invalid username {username}.");
        }

        if (parameters.Limit <= 0)
        {
            throw new ArgumentException($"Invalid limit {parameters.Limit}. Limit must be greater or equal to 1.");
        }

        if (parameters.LastSeenConversationTime < 0)
        {
            throw new ArgumentException(
                $"Invalid lastSeenConversationTime {parameters.LastSeenConversationTime}. lastSeenConversationTime must be greater or equal to 0.");
        }
        
        await ThrowIfParticipantNotFound(username);
        
        var result = await _userConversationStore.GetUserConversations(username, parameters);

        List<Conversation> conversations = await UserConversationsToConversations(result.UserConversations);

        return new GetConversationsResult
        {
            Conversations = conversations,
            NextContinuationToken = result.NextContinuationToken
        };
    }

    private async Task<List<Conversation>> UserConversationsToConversations(List<UserConversation> userConversations)
    {
        Conversation[] conversations = new Conversation[userConversations.Count];
        
        await Task.WhenAll(userConversations.Select(async (userConversation, index) =>
        {
            string[] usernames = ConversationIdUtilities.SplitConversationId(userConversation.ConversationId);
            string recipientUsername = GetRecipientUsername(senderUsername: userConversation.Username, usernames);
            
            Profile recipientProfile = await _profileService.GetProfile(recipientUsername);

            Conversation conversation = new()
            {
                ConversationId = userConversation.ConversationId,
                LastModifiedUnixTime = userConversation.LastModifiedTime,
                Recipient = recipientProfile
            };
            conversations[index] = conversation;
        }));
        
        return conversations.ToList();
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
    
    private async Task CreateUserConversation(string username, string conversationId, long lastModifiedTime)
    {
        UserConversation userConversation = new()
        {
            Username = username,
            ConversationId = conversationId,
            LastModifiedTime = lastModifiedTime
        };
        await _userConversationStore.CreateUserConversation(userConversation);
    }
    
    private async Task EnsureThatParticipantsExist(List<string> participants)
    {
        await Task.WhenAll(participants.Select(ThrowIfParticipantNotFound));
    }

    private async Task ThrowIfParticipantNotFound(string username)
    {
        bool profileExists = await _profileService.ProfileExists(username);
        if (!profileExists)
        {
            throw new UserNotFoundException($"A user with the username {username} was not found.");
        }
    }
    
    private string GetRecipientUsername(string senderUsername, string[] usernames)
    {
        if (senderUsername.Equals(usernames[0]))
        {
           return usernames[1];
        }
        return usernames[0];
    }
}