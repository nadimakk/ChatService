using System.ComponentModel.DataAnnotations;

namespace ChatService.Web.Dtos;

public record GetConversationsResponse
{
    [Required] public List<Conversation> conversations { get; set; }
    [Required] public string nextContinuationToken { get; set; }
}