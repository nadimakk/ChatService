namespace ChatService.Web.Configuration;

public record BlobStorageSettings
{
    public string ConnectionString { get; init; }
}