namespace ChatService.Web.Dtos;

public record Image(
    string ContentType, 
    MemoryStream Content);