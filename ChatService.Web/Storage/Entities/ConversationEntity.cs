using Microsoft.Azure.Cosmos.Serialization.HybridRow;

namespace ChatService.Web.Storage.Entities;

public record ConversationEntity(
    string partitionKey,
    string id,
    UnixDateTime lastModifiedTime,
    List<string> participants
    );