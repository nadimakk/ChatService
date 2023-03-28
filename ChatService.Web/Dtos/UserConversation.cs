using Microsoft.Azure.Cosmos.Serialization.HybridRow;

namespace ChatService.Web.Dtos;

public record UserConversation
{
    public string username { get; set; }
    public string conversationId { get; set; }
    public long lastModifiedTime { get; set; }
};