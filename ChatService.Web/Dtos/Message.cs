using System.ComponentModel.DataAnnotations;
using Microsoft.Azure.Cosmos.Serialization.HybridRow;

namespace ChatService.Web.Dtos;

public record Message
{
    [Required] public string id { get; set; }
    [Required] public long unixTime { get; set; }
    [Required] public string senderUsername { get; set; }
    [Required] public string text { get; set; }
};