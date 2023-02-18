using System.ComponentModel.DataAnnotations;

namespace ChatService.Web.Dtos;

public record Profile(
    [Required] string username, 
    [Required] string firstName, 
    [Required] string lastName,
    [Required] string profilePictureId);