using System.ComponentModel.DataAnnotations;

namespace ChatService.Web.Dtos;

public record StartConversationRequest
{
    [Required] public List<string> Participants { get; set; }
    [Required] public SendMessageRequest FirstMessage { get; set; }
}