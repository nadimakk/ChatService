using ChatService.Web.Dtos;
using ChatService.Web.Enums;

namespace ChatService.Web.Storage;

public interface IMessageStore
{
    Task AddMessage(string conversationId, Message message);
    Task<Message> GetMessage(string conversationId, string messageId);
    Task<(List<Message> Messages, string NextContinuationToken)> GetMessages(
        string conversationId, int limit, OrderBy order, string? continuationToken, long lastSeenMessageTime);
    Task DeleteMessage(string conversationId, string messageId);
}
