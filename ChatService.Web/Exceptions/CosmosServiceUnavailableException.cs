namespace ChatService.Web.Exceptions;

public class CosmosServiceUnavailableException : Exception
{
    public CosmosServiceUnavailableException(string? message) : base(message)
    {
    }
}