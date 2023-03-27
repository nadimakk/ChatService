namespace ChatService.Web.Dtos;

public record UserConversation(
    string username,
    string conversationId
    );