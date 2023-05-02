using System.ComponentModel.DataAnnotations;
using ChatService.Web.Enums;

namespace ChatService.Web.Dtos;

public record GetConversationsParameters
{
    [Required] public string Username { get; set; }
    [Required] public int Limit { get; set; } 
    [Required] public OrderBy Order { get; set; } 
    public string? ContinuationToken { get; set; }
    [Required] public long LastSeenConversationTime { get; set; }
};