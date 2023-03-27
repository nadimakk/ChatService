using System.ComponentModel.DataAnnotations;
using Microsoft.Azure.Cosmos.Serialization.HybridRow;

namespace ChatService.Web.Dtos;

public record Message(
    [Required] string id,
    [Required] UnixDateTime UnixTime,
    [Required] string senderUsername,
    [Required] string text
);