using ChatService.Web.Dtos;
using ChatService.Web.Storage.Entities;

namespace ChatService.Web.Storage;

public class CosmosMessageStore
{
    private static MessageEntity ToEntity(string conversationId, Message message)
    {
        return new MessageEntity(
            partitionKey: conversationId,
            id: message.id,
            message.UnixTime,
            message.senderUsername,
            message.text
        );
    }

    private static Message ToMessage(MessageEntity entity)
    {
        return new Message(
            id: entity.id,
            entity.UnixTime,
            entity.senderUsername,
            entity.text
        );
    }
}