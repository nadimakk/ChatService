using ChatService.Web.Dtos;

namespace ChatService.Web.Storage;

public interface IUserConversationStore
{
    Task CreateUserConversation(UserConversation userConversation);
    Task<UserConversation?> GetUserConversation(string username, string conversationId);
    Task<GetUserConversationsResult> GetUserConversations(string username, GetUserConversationsParameters parameters);
    Task DeleteUserConversation(string username, string conversationId);
}