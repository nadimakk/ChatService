using ChatService.Web.Dtos;
using ChatService.Web.Enums;
using ChatService.Web.Exceptions;
using ChatService.Web.Storage;
using ChatService.Web.Utilities;

namespace ChatService.Web.Services;

public class MessageService : IMessageService
{
    private readonly IMessageStore _messageStore;
    private readonly IUserConversationStore _userConversationStore;
    private readonly IProfileService _profileService;

    public MessageService(IMessageStore messageStore, IUserConversationStore userConversationStore, 
        IProfileService profileService)
    {
        _messageStore = messageStore;
        _userConversationStore = userConversationStore;
        _profileService = profileService;
    }

    public async Task<SendMessageResponse> AddMessage(string conversationId, bool isFirstMessage,
        SendMessageRequest request)
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
        catch (MessageExistsException e)
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

    public async Task<GetMessagesResult> GetMessages(string conversationId, GetMessagesParameters parameters)
    {
        ValidateConversationId(conversationId);
        ValidateLimit(parameters.Limit);
        ValidateLastSeenConversationTime(parameters.LastSeenMessageTime);
        await CheckIfConversationExists(conversationId);
        
        return await _messageStore.GetMessages(conversationId, parameters);
    }

    private async Task UpdateUserConversationsLastModifiedTime(string conversationId, long unixTime)
    {
        string[] usernames = ConversationIdUtilities.SplitConversationId(conversationId);
        UserConversation userConversation1 = CreateUserConversationObject(usernames[0], conversationId, 
            lastModifiedTime: unixTime);
        UserConversation userConversation2 = CreateUserConversationObject(usernames[1], conversationId, 
            lastModifiedTime: unixTime);
        
        await Task.WhenAll(
            _userConversationStore.UpsertUserConversation(userConversation1),
            _userConversationStore.UpsertUserConversation(userConversation2));
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
        ConversationIdUtilities.ValidateConversationId(conversationId);
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
    
    private async Task ThrowIfUserNotFound(string username)
    {
        bool profileExists = await _profileService.ProfileExists(username);
        if (!profileExists)
        {
            throw new UserNotFoundException($"A user with the username {username} was not found.");
        }
    }
    
    private void AuthorizeSender(string conversationId, string senderUsername)
    {
        string[] usernames = ConversationIdUtilities.SplitConversationId(conversationId);
        bool userNotParticipant = !usernames[0].Equals(senderUsername) && !usernames[1].Equals(senderUsername);
        if (userNotParticipant)
        {
            throw new UserNotParticipantException(
                $"User {senderUsername} is not a participant of conversation {conversationId}.");
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

    private UserConversation CreateUserConversationObject(string username, string conversationId, long lastModifiedTime)
    {
        return new UserConversation
        {
            Username = username,
            ConversationId = conversationId,
            LastModifiedTime = lastModifiedTime
        };
    }
}