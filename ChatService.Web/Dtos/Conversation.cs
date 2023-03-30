using System.ComponentModel.DataAnnotations;
using Microsoft.Azure.Cosmos.Serialization.HybridRow;

namespace ChatService.Web.Dtos;

public record Conversation
{
    [Required] public string id { get; set; }
    [Required] public long lastModifiedUnixTime { get; set; }
    [Required] public Profile recipient { get; set; }
}