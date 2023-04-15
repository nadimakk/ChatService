using System.ComponentModel.DataAnnotations;

namespace ChatService.Web.Dtos;

public record GetMessagesResponse
{
    [Required] public List<Message> Messages { get; set; }
    [Required] public string NextUri { get; set; }
}