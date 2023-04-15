using ChatService.Web.Dtos;
using ChatService.Web.Enums;
using ChatService.Web.Exceptions;
using ChatService.Web.Storage;
using ChatService.Web.Utilities;

namespace ChatService.Web.Services;

public class MessageService : IMessageService
{
    private readonly IMessageStore _messageStore;
    private readonly IProfileService _profileService;

    public MessageService(IMessageStore messageStore, IProfileService profileService)
    {
        _messageStore = messageStore;
        _profileService = profileService;
    }

    public async Task<SendMessageResponse> AddMessage(string conversationId, bool isFirstMessage,
        SendMessageRequest request)
    {
        ValidateSendMessageRequest(request);
        ValidateConversationId(conversationId);
        // if (!isFirstMessage)
        // {
        //     await CheckIfConversationExists(conversationId);
        // }
        // await ThrowIfUserNotFound(request.SenderUsername);
        
        AuthorizeSender(conversationId, request.SenderUsername);
        
        long unixTimeNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        Message message = new()
        {
            Id = request.Id,
            UnixTime = unixTimeNow,
            SenderUsername = request.SenderUsername,
            Text = request.Text
        };

        await _messageStore.AddMessage(conversationId, message);

        return new SendMessageResponse
        {
            CreatedUnixTime = unixTimeNow
        };
    }

    public async Task<SendMessageResponse> AddFirstMessage(string conversationId, SendMessageRequest request)
    {
        return await AddMessage(conversationId, isFirstMessage: true, request);
    }
    
    public async Task<GetMessagesResult> GetMessages(string conversationId, GetMessagesParameters parameters)
    {
        ValidateConversationId(conversationId);
        ValidateLimit(parameters.Limit);
        ValidateLastSeenConversationTime(parameters.LastSeenMessageTime);
        // await CheckIfConversationExists(conversationId);
        
        return await _messageStore.GetMessages(conversationId, parameters);
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
        bool conversationExists = await _messageStore.ConversationPartitionExists(conversationId);
        if (!conversationExists)
        {
            throw new ConversationDoesNotExistException(
                $"A conversation partition with the conversationId {conversationId} does not exist.");
        }
    }
}