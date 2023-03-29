using ChatService.Web.Dtos;
using ChatService.Web.Enums;
using Microsoft.Azure.Cosmos.Serialization.HybridRow;

namespace ChatService.Web.Storage;

public interface IConversationStore
{
    Task CreateUserConversation(UserConversation userConversation);
    Task<UserConversation> GetUserConversation(string username, string conversationId);
    Task<(List<UserConversation> UserConversations, string NextContinuationToken)> GetUserConversations(
        string username, int limit, OrderBy order, string? continuationToken, long lastSeenConversationTime);
    Task DeleteUserConversation(string username, string conversationId);
}