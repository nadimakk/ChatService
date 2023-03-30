using ChatService.Web.Dtos;
using ChatService.Web.Enums;

namespace ChatService.Web.Services;

public class MessageService : IMessageService
{
    public Task<SendMessageResponse> AddMessage(SendMessageRequest request)
    {
        throw new NotImplementedException();
    }

    public Task<GetMessagesResponse> GetMessages(string conversationId, int limit, OrderBy orderBy, string? continuationToken,
        long lastSeenConversationTime)
    {
        throw new NotImplementedException();
    }
}