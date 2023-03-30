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
        if (request == null ||
            string.IsNullOrWhiteSpace(request.MessageId) ||
            string.IsNullOrWhiteSpace(request.SenderUsername) ||
            string.IsNullOrWhiteSpace(request.Text)
           )
        {
            throw new ArgumentException($"Invalid SendMessageRequest {request}.");
        }

        if (string.IsNullOrWhiteSpace(conversationId))
        {
            throw new ArgumentException($"Invalid conversationId {conversationId}.");
        }

        //check if converstionId contains sender username to know if they are allowed to send message here
        if (!conversationId.Contains(request.SenderUsername))
        {
            //TODO: 403 error code in controller
            throw new UserNotParticipantException(
                $"User {request.SenderUsername} is not a participant of conversation {conversationId}.");
        }

        //check if the sender's profile exists
        if (!await _profileService.ProfileExists(request.SenderUsername))
        {
            throw new ProfileNotFoundException(
                $"A profile with the username {request.SenderUsername} was not found.");
        }

        //if this is NOT the first message, check if the conversation already exists
        if (!isFirstMessage && !await _messageStore.ConversationPartitionExists(conversationId))
        {
            throw new ConversationDoesNotExistException(
                $"A conversation partition with the conversationId {conversationId} does not exist.");
        }
        //if it IS the first message, then its ok if the conversation does not exist as the partition will be created

        //add the message to the conversation partition
        long unixTimeNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        Message message = new Message
        {
            MessageId = request.MessageId,
            UnixTime = unixTimeNow,
            SenderUsername = request.SenderUsername,
            Text = request.Text
        };

        await _messageStore.AddMessage(conversationId, message);
        ////////////////////////////////////

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
        if (string.IsNullOrWhiteSpace(conversationId))
        {
            throw new ArgumentException($"Invalid conversationId {conversationId}.");
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
}