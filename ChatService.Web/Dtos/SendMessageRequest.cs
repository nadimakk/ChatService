using System.ComponentModel.DataAnnotations;

namespace ChatService.Web.Dtos;

public record SendMessageRequest
{
    [Required] public string id { get; set; }
    [Required] public string SenderUsername { get; set; }
    [Required] public string text { get; set; }
}