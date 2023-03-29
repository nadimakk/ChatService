using System.ComponentModel.DataAnnotations;
using Microsoft.Azure.Cosmos.Serialization.HybridRow;

namespace ChatService.Web.Dtos;

public record Conversation
{
    [Required] private string id { get; set; }
    [Required] long lastModifiedUnixTime { get; set; }
    [Required] private Profile recipient { get; set; }
}