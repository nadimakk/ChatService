using System.ComponentModel.DataAnnotations;

namespace ChatService.Web.Dtos;

public record Conversation
{
    // [Required] public string ConversationId { get; set; }
    [Required] public string Id { get; set; }
    [Required] public long LastModifiedUnixTime { get; set; }
    [Required] public Profile Recipient { get; set; }
}