using System.ComponentModel.DataAnnotations;

namespace ChatService.Web.Dtos;

public record StartConversationResponse
{
    // [Required] public string ConversationId { get; set; }
    [Required] public string Id { get; set; }
    [Required] public long CreatedUnixTime { get; set; }
}