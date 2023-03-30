using ChatService.Web.Dtos;
using ChatService.Web.Enums;
using ChatService.Web.Exceptions;
using ChatService.Web.Storage;

namespace ChatService.Web.Services;

public class MessageService : IMessageService
{
    private readonly IMessageStore _messageStore;
    private readonly IProfileService _profileService;
    private readonly IConversationService _conversationService;

    public MessageService(IMessageStore messageStore, IProfileService profileService,
        IConversationService conversationService)
    {
        _messageStore = messageStore;
        _profileService = profileService;
        _conversationService = conversationService;
    }

    public async Task<SendMessageResponse> AddMessage(string conversationId, bool isFirstMessage,
        SendMessageRequest request)
    {
        if (request == null ||
            string.IsNullOrEmpty(request.id) ||
            string.IsNullOrEmpty(request.SenderUsername) ||
            string.IsNullOrEmpty(request.text)
           )
        {
            throw new ArgumentException($"Invalid SendMessageRequest {request}.");
        }

        if (string.IsNullOrEmpty(conversationId))
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
            throw new ConversationPartitionDoesNotExist(
                $"A conversation partition with the conversationId {conversationId} does not exist.");
        }
        //if it IS the first message, then its ok if the conversation does not exist as the partition will be created

        //add the message to the conversation partition
        long unixTimeNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        Message message = new Message
        {
            id = request.id,
            unixTime = unixTimeNow,
            senderUsername = request.SenderUsername,
            text = request.text
        };

        await _messageStore.AddMessage(conversationId, message);
        ////////////////////////////////////

        return new SendMessageResponse
        {
            CreatedUnixTime = unixTimeNow
        };
    }

    public async Task<GetMessagesResponse> GetMessages(string conversationId, int limit, OrderBy orderBy,
        string? continuationToken, long lastSeenConversationTime)
    {
        if (string.IsNullOrEmpty(conversationId))
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

        var result = await _messageStore.GetMessages(
            conversationId, limit, orderBy, continuationToken, lastSeenConversationTime);

        return new GetMessagesResponse
        {
            messages = result.Messages,
            nextContinuationToken = result.NextContinuationToken
        };
    }
}