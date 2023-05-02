using ChatService.Web.Dtos;

namespace ChatService.Web.Services;

public interface IConversationService
{
    Task<StartConversationResult> StartConversation(StartConversationRequest request);
    Task<GetConversationsResult> GetConversations(GetConversationsParameters parameters);
    Task<SendMessageResponse> AddMessage(string conversationId, bool isFirstMessage, SendMessageRequest request);
    Task<GetMessagesResult> GetMessages(GetMessagesParameters parameters);
}