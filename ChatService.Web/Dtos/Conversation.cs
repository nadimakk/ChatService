using System.ComponentModel.DataAnnotations;
using Microsoft.Azure.Cosmos.Serialization.HybridRow;

namespace ChatService.Web.Dtos;

public record Conversation(
    [Required] string id,
    [Required] UnixDateTime lastModifiedTime,
    [Required] List<string> participants
    );