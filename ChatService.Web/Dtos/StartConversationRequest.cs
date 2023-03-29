using System.ComponentModel.DataAnnotations;

namespace ChatService.Web.Dtos;

public record StartConversationRequest
{
    [Required] public List<string> participants { get; set; }
    [Required] public SendMessageRequest firstMessage { get; set; }
}