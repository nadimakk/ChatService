using System.ComponentModel.DataAnnotations;

namespace ChatService.Web.Dtos;

public record GetUserConversationsResult
{
    [Required] public List<UserConversation> UserConversations { get; set; }
    [Required] public string? NextContinuationToken { get; set; }
}