using ChatService.Web.Dtos;
using ChatService.Web.Enums;

namespace ChatService.Web.Services;

public interface IMessageService
{
    Task<SendMessageResponse> AddMessage(SendMessageRequest request);
    Task<GetMessagesResponse> GetMessages(
        string conversationId, int limit, OrderBy orderBy, string? continuationToken, long lastSeenConversationTime);
}