using ChatService.Web.Dtos;
using ChatService.Web.Enums;

namespace ChatService.Web.Storage;

public interface IMessageStore
{
    Task AddMessage(string conversationId, Message message);
    Task<Message?> GetMessage(string conversationId, string messageId);
    Task<GetMessagesResult> GetMessages(string conversationId, GetMessagesParameters parameters);
    Task<bool> ConversationPartitionExists(string conversationId);
    Task DeleteMessage(string conversationId, string messageId);
}
