using ChatService.Web.Dtos;
using ChatService.Web.Enums;

namespace ChatService.Web.Services;

public interface IUserConversationService
{
    Task<StartConversationResult> StartConversation(StartConversationRequest request);
    Task<GetConversationsResult> GetUserConversations(string username, GetUserConversationsParameters parameters);
}