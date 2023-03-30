using ChatService.Web.Dtos;
using ChatService.Web.Enums;

namespace ChatService.Web.Services;

public interface IMessageService
{
    Task<SendMessageResponse> AddMessage(string conversationId, bool isFirstMessage, SendMessageRequest request);
    Task<SendMessageResponse> AddFirstMessage(string conversationId, SendMessageRequest request);
    Task<GetMessagesServiceResult> GetMessages(
        string conversationId, int limit, OrderBy orderBy, string? continuationToken, long lastSeenConversationTime);
}