using ChatService.Web.Dtos;
using ChatService.Web.Enums;

namespace ChatService.Web.Services;

public interface IUserConversationService
{
    Task<StartConversationServiceResult> CreateConversation(StartConversationRequest request);
    Task<GetUserConversationsServiceResult> GetUserConversations(
        string username, int limit, OrderBy orderBy, string? continuationToken, long lastSeenConversationTime);
}