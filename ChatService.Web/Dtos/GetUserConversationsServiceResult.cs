using System.ComponentModel.DataAnnotations;

namespace ChatService.Web.Dtos;

public record GetUserConversationsServiceResult
{
    [Required] public List<Conversation> Conversations { get; set; }
    [Required] public string? NextContinuationToken { get; set; }
}