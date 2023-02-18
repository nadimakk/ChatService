namespace ChatService.Web.Configuration;

public class BlobSettings
{
    public string ConnectionString { get; init; }
    public Uri ContainerUri { get; init; }
}