using System.ComponentModel.DataAnnotations;

namespace ChatService.Web.Dtos;

public record Message
{
    // [Required] public string MessageId { get; set; }
    [Required] public string Id { get; set; }
    [Required] public long UnixTime { get; set; }
    [Required] public string SenderUsername { get; set; }
    [Required] public string Text { get; set; }
};