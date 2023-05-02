using ChatService.Web.Dtos;

namespace ChatService.Web.Storage;

public interface IUserConversationStore
{
    Task UpsertUserConversation(UserConversation userConversation);
    Task<UserConversation?> GetUserConversation(string username, string conversationId);
    Task<GetUserConversationsResult> GetUserConversations(GetUserConversationsParameters parameters);
    Task DeleteUserConversation(string username, string conversationId);
}