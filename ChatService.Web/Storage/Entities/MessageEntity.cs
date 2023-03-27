using Microsoft.Azure.Cosmos.Serialization.HybridRow;

namespace ChatService.Web.Storage.Entities;

public record MessageEntity(
    string partitionKey,
    string id,
    UnixDateTime UnixTime,
    string senderUsername,
    string text
    );