using System.ComponentModel.DataAnnotations;

namespace ChatService.Web.Dtos;

public record GetConversationsResult
{
    [Required] public List<Conversation> Conversations { get; set; }
    [Required] public string? NextContinuationToken { get; set; }
}