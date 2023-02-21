namespace ChatService.Web.Dtos;

public record ImageDto(
    string ContentType, 
    MemoryStream Content);