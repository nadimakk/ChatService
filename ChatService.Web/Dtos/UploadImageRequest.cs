using System.ComponentModel.DataAnnotations;

namespace ChatService.Web.Dtos;

public record UploadImageRequest(
    [Required] IFormFile File);