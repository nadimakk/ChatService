using System.ComponentModel.DataAnnotations;

namespace ChatService.Web.Dtos;

public record Profile
{
    [Required] public string Username { get; set; }
    [Required] public string FirstName { get; set; }
    [Required] public string LastName { get; set; }
    public string? ProfilePictureId { get; set; }
}