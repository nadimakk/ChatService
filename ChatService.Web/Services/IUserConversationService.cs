using ChatService.Web.Dtos;
using ChatService.Web.Enums;

namespace ChatService.Web.Services;

public interface IUserConversationService
{
    Task<StartConversationResult> CreateConversation(StartConversationRequest request);
    Task<GetConversationsResult> GetUserConversations(string username, GetUserConversationsParameters parameters);
}