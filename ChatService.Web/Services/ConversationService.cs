using ChatService.Web.Dtos;
using ChatService.Web.Exceptions;
using ChatService.Web.Storage;
using ChatService.Web.Utilities;

namespace ChatService.Web.Services;

public class ConversationService : IConversationService
{
    private readonly IUserConversationStore _userConversationStore;
    private readonly IMessageStore _messageStore;
    private readonly IProfileService _profileService;
    private static readonly char Seperator = '_';
    
    public ConversationService(IUserConversationStore userConversationStore, IMessageStore messageStore, IProfileService profileService)
    {
        _userConversationStore = userConversationStore;
        _messageStore = messageStore;
        _profileService = profileService;
    }

    public async Task<StartConversationResult> StartConversation(StartConversationRequest request)
    {
        ValidateStartConversationRequest(request);
        await EnsureThatParticipantsExist(request.Participants);
        
        string username1 = request.Participants.ElementAt(0);
        string username2 = request.Participants.ElementAt(1);
        string conversationId = GenerateConversationId(username1, username2);

        SendMessageRequest sendMessageRequest = new()
        {
            Id = request.FirstMessage.Id,
            SenderUsername = request.FirstMessage.SenderUsername,
            Text = request.FirstMessage.Text
        };
        await AddMessage(conversationId, isFirstMessage: true, sendMessageRequest);
        
        long unixTimeNow = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        
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

    public async Task<GetConversationsResult> GetConversations(GetConversationsParameters parameters)
    {
        ValidateGetConversationsParameters(parameters);

        await ThrowIfUserNotFound(parameters.Username);
        
        var result = await _userConversationStore.GetUserConversations(parameters);

        List<Conversation> conversations = await UserConversationsToConversations(result.UserConversations);

        return new GetConversationsResult
        {
            Conversations = conversations,
            NextContinuationToken = result.NextContinuationToken
        };
    }

    public async Task<SendMessageResponse> AddMessage(string conversationId, bool isFirstMessage, SendMessageRequest request)
    {
        ValidateSendMessageRequest(request);
        ValidateConversationId(conversationId);
        if (!isFirstMessage)
        {
            await CheckIfConversationExists(conversationId);
        }
        await ThrowIfUserNotFound(request.SenderUsername);
        
        AuthorizeSender(conversationId, request.SenderUsername);
        
        long unixTimeNow = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        Message message = new()
        {
            Id = request.Id,
            UnixTime = unixTimeNow,
            SenderUsername = request.SenderUsername,
            Text = request.Text
        };

        try
        {
            await _messageStore.AddMessage(conversationId, message);
        }
        catch (MessageExistsException)
        {
            await _messageStore.UpdateMessageTime(conversationId, message);
            throw;
        }

        await UpdateUserConversationsLastModifiedTime(conversationId, unixTimeNow);
        
        return new SendMessageResponse
        {
            CreatedUnixTime = unixTimeNow
        };
    }

    public async Task<GetMessagesResult> GetMessages(GetMessagesParameters parameters)
    {
        ValidateConversationId(parameters.ConversationId);
        ValidateLimit(parameters.Limit);
        ValidateLastSeenConversationTime(parameters.LastSeenMessageTime);
        await CheckIfConversationExists(parameters.ConversationId);
        
        return await _messageStore.GetMessages(parameters.ConversationId, parameters);
    }
    
    private async Task UpdateUserConversationsLastModifiedTime(string conversationId, long unixTime)
    {
        string[] usernames = SplitConversationId(conversationId);
        UserConversation userConversation1 = CreateUserConversationObject(usernames[0], conversationId, 
            lastModifiedTime: unixTime);
        UserConversation userConversation2 = CreateUserConversationObject(usernames[1], conversationId, 
            lastModifiedTime: unixTime);
        
        await Task.WhenAll(
            _userConversationStore.UpsertUserConversation(userConversation1),
            _userConversationStore.UpsertUserConversation(userConversation2));
    }
    
    private UserConversation CreateUserConversationObject(string username, string conversationId, long lastModifiedTime)
    {
        return new UserConversation
        {
            Username = username,
            ConversationId = conversationId,
            LastModifiedTime = lastModifiedTime
        };
    }
    
    private void AuthorizeSender(string conversationId, string senderUsername)
    {
        string[] usernames = SplitConversationId(conversationId);
        bool userNotParticipant = !usernames[0].Equals(senderUsername) && !usernames[1].Equals(senderUsername);
        if (userNotParticipant)
        {
            throw new UserNotParticipantException(
                $"User {senderUsername} is not a participant of conversation {conversationId}.");
        }
    }
    
    private async Task EnsureThatParticipantsExist(List<string> participants)
    {
        await Task.WhenAll(participants.Select(ThrowIfUserNotFound));
    }
    
    private async Task ThrowIfUserNotFound(string username)
    {
        bool profileExists = await _profileService.ProfileExists(username);
        if (!profileExists)
        {
            throw new UserNotFoundException($"A user with the username {username} was not found.");
        }
    }
    
    private async Task CheckIfConversationExists(string conversationId)
    {
        bool conversationExists = await _messageStore.ConversationExists(conversationId);
        if (!conversationExists)
        {
            throw new ConversationDoesNotExistException(
                $"A conversation partition with the conversationId {conversationId} does not exist.");
        }
    }
    
    private string GenerateConversationId(string username1, string username2)
    {
        if (username1.CompareTo(username2) < 0)
        {
            return username1 + Seperator + username2;
        }
        return username2 + Seperator + username1;
    }
    
    private async Task CreateUserConversation(string username, string conversationId, long lastModifiedTime)
    {
        UserConversation userConversation = new()
        {
            Username = username,
            ConversationId = conversationId,
            LastModifiedTime = lastModifiedTime
        };
        await _userConversationStore.UpsertUserConversation(userConversation);
    }
    
    private async Task<List<Conversation>> UserConversationsToConversations(List<UserConversation> userConversations)
    {
        Conversation[] conversations = new Conversation[userConversations.Count];
        
        await Task.WhenAll(userConversations.Select(async (userConversation, index) =>
        {
            string[] usernames = SplitConversationId(userConversation.ConversationId);
            string recipientUsername = GetRecipientUsername(senderUsername: userConversation.Username, usernames);
            
            Profile recipientProfile = await _profileService.GetProfile(recipientUsername);

            Conversation conversation = new()
            {
                Id = userConversation.ConversationId,
                LastModifiedUnixTime = userConversation.LastModifiedTime,
                Recipient = recipientProfile
            };
            conversations[index] = conversation;
        }));
        
        return conversations.ToList();
    }
    
    private string[] SplitConversationId(string conversationId)
    {
        return conversationId.Split(Seperator);
    }
    
    private string GetRecipientUsername(string senderUsername, string[] usernames)
    {
        if (senderUsername.Equals(usernames[0]))
        {
            return usernames[1];
        }
        return usernames[0];
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

        if (string.IsNullOrWhiteSpace(request.FirstMessage.Id) ||
            string.IsNullOrWhiteSpace(request.FirstMessage.SenderUsername) ||
            string.IsNullOrWhiteSpace(request.FirstMessage.Text))
        {
            throw new ArgumentException($"Invalid FirstMessage {request.FirstMessage}.");
        }
    }
    
    private void ValidateGetConversationsParameters(GetConversationsParameters parameters)
    {
        if (string.IsNullOrWhiteSpace(parameters.Username))
        {
            throw new ArgumentException($"Invalid username {parameters.Username}.");
        }

        if (parameters.Limit <= 0)
        {
            throw new ArgumentException($"Invalid limit {parameters.Limit}. Limit must be greater or equal to 1.");
        }

        if (parameters.LastSeenConversationTime < 0)
        {
            throw new ArgumentException($"Invalid lastSeenConversationTime {parameters.LastSeenConversationTime}. " +
                                        $"lastSeenConversationTime must be greater or equal to 0.");
        }
    }
    
    private void ValidateSendMessageRequest(SendMessageRequest request)
    {
        if (request == null ||
            string.IsNullOrWhiteSpace(request.Id) ||
            string.IsNullOrWhiteSpace(request.SenderUsername) ||
            string.IsNullOrWhiteSpace(request.Text)
           )
        {
            throw new ArgumentException($"Invalid SendMessageRequest {request}.");
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

    private void ValidateLastSeenConversationTime(long lastSeenConversationTime)
    { 
        if (lastSeenConversationTime < 0) 
        { 
            throw new ArgumentException($"Invalid lastSeenConversationTime {lastSeenConversationTime}. " +
                                        $"LastSeenConversationTime must be greater or equal to 0."); 
        }
    }
}