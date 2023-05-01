using ChatService.Web.Dtos;

namespace ChatService.Web.Services;

public interface IMessageService
{
    Task<SendMessageResponse> AddMessage(string conversationId, bool isFirstMessage, SendMessageRequest request);
    Task<GetMessagesResult> GetMessages(string conversationId, GetMessagesParameters parameters);
}