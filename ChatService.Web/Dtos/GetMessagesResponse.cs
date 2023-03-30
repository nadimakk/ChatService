using System.ComponentModel.DataAnnotations;

namespace ChatService.Web.Dtos;

public record GetMessagesResponse
{
    [Required] public List<Message> messages { get; set; }
    [Required] public string nextContinuationToken { get; set; }
}