namespace ChatService.Web.Configuration;

public record BlobSettings
{
    public string ConnectionString { get; init; }
}