using ChatService.Web.Dtos;

namespace ChatService.Web.Storage;

public interface IMessageStore
{
    Task AddMessage(Message message);
    Task GetMessages(string conversationId, string continuationToken, int limit);
}