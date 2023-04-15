using System.ComponentModel.DataAnnotations;

namespace ChatService.Web.Dtos;

public record GetUserConversationsResponse
{
    [Required] public List<Conversation> Conversations { get; set; }
    [Required] public string NextUri { get; set; }
}