using System.ComponentModel.DataAnnotations;

namespace ChatService.Web.Dtos;

public record GetMessagesServiceResult
{
    [Required] public List<Message> Messages { get; set; }
    [Required] public string? NextContinuationToken { get; set; }
}