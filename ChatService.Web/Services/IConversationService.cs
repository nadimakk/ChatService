using ChatService.Web.Dtos;
using ChatService.Web.Enums;

namespace ChatService.Web.Services;

public interface IConversationService
{
    Task<StartConversationResponse> CreateConversation(StartConversationRequest request);
    Task<GetConversationsResponse> GetConversations(
        string username, int limit, OrderBy orderBy, string? continuationToken, long lastSeenConversationTime);
}