using System.ComponentModel.DataAnnotations;

namespace ChatService.Web.Dtos;

public record Conversation
{
    [Required] public string Id { get; set; }
    [Required] public long LastModifiedUnixTime { get; set; }
    [Required] public Profile Recipient { get; set; }
}