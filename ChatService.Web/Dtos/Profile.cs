using System.ComponentModel.DataAnnotations;

namespace ChatService.Web.Dtos;

public record Profile(
    [Required] string Username, 
    [Required] string FirstName, 
    [Required] string LastName,
    [Required] string ProfilePictureId);