namespace ChatService.Web.Storage.Entities;

public record UserConversationEntity(
    string partitionKey,
    string id
    );