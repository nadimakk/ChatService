using ChatService.Web.Dtos;

namespace ChatService.Web.Storage;

public interface IConversationStore
{
    Task CreateConversation(Conversation conversation);
    Task CreateUserConversation(UserConversation userConversation);
}