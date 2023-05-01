using System.ComponentModel.DataAnnotations;

namespace ChatService.Web.Dtos;

public record SendMessageRequest
{
    [Required] public string Id { get; set; }
    [Required] public string SenderUsername { get; set; }
    [Required] public string Text { get; set; }
}