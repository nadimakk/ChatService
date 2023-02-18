namespace ChatService.Web.Configuration;

public record CosmosSettings
{
    public string ConnectionString { get; init; }
}