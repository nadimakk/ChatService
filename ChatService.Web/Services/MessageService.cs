using ChatService.Web.Dtos;
using ChatService.Web.Enums;
using ChatService.Web.Exceptions;
using ChatService.Web.Storage;

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
        
        if (!isFirstMessage && !await _messageStore.ConversationPartitionExists(conversationId))
        {
            throw new ConversationDoesNotExistException(
                $"A conversation partition with the conversationId {conversationId} does not exist.");
        }

        if (!await _profileService.ProfileExists(request.SenderUsername))
        {
            throw new ProfileNotFoundException(
                $"A profile with the username {request.SenderUsername} was not found.");
        }
        
        AuthorizeSender(conversationId, request.SenderUsername);
        
        long unixTimeNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        Message message = new Message
        {
            MessageId = request.MessageId,
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
        return await AddMessage(conversationId, true, request);
    }
    
    public async Task<GetMessagesServiceResult> GetMessages(string conversationId, int limit, OrderBy orderBy,
        string? continuationToken, long lastSeenConversationTime)
    {
        ValidateConversationId(conversationId);
        ValidateLimit(limit);
        ValidateLastSeenConversationTime(lastSeenConversationTime);

        if (!await _messageStore.ConversationPartitionExists(conversationId))
        {
            throw new ConversationDoesNotExistException(
                $"A conversation partition with the conversationId {conversationId} does not exist.");
        }

        var result = await _messageStore.GetMessages(
            conversationId, limit, orderBy, continuationToken, lastSeenConversationTime);

        return new GetMessagesServiceResult
        {
            Messages = result.Messages,
            NextContinuationToken = result.NextContinuationToken
        };
    }

    private void ValidateSendMessageRequest(SendMessageRequest request)
    {
        if (request == null ||
            string.IsNullOrWhiteSpace(request.MessageId) ||
            string.IsNullOrWhiteSpace(request.SenderUsername) ||
            string.IsNullOrWhiteSpace(request.Text)
           )
        {
            throw new ArgumentException($"Invalid SendMessageRequest {request}.");
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

    private void ValidateLastSeenConversationTime(long lastSeenConversationTime)
    { 
        if (lastSeenConversationTime < 0) 
        { 
            throw new ArgumentException($"Invalid lastSeenConversationTime {lastSeenConversationTime}. " +
                                        $"LastSeenConversationTime must be greater or equal to 0."); 
        }
    }
    
    private void AuthorizeSender(string conversationId, string senderUsername)
    {
        string[] usernames = conversationId.Split('_');
        if (!usernames[0].Equals(senderUsername) && !usernames[1].Equals(senderUsername))
        {
            throw new UserNotParticipantException(
                $"User {senderUsername} is not a participant of conversation {conversationId}.");
        }
    }
}