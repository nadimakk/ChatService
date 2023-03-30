namespace ChatService.Web.Storage.Entities;

public record MessageEntity(
    string partitionKey,
    string id,
    long UnixTime,
    string SenderUsername,
    string Text);