namespace ChatService.Web.Dtos;

public record UserConversation
{
    public string Username { get; set; }
    public string ConversationId { get; set; }
    public string OtherParticipantUsername { get; set; }
    public long LastModifiedTime { get; set; }
};