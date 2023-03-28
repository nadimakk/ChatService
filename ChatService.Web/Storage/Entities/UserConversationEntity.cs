using Microsoft.Azure.Cosmos.Serialization.HybridRow;

namespace ChatService.Web.Storage.Entities;

public record UserConversationEntity(
    string partitionKey,
    string id,
    long lastModifiedTime
    );