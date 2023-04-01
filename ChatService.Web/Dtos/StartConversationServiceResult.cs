using System.ComponentModel.DataAnnotations;

namespace ChatService.Web.Dtos;

public record StartConversationServiceResult
{
    [Required] public string ConversationId { get; set; }
    [Required] public long CreatedUnixTime { get; set; }
}