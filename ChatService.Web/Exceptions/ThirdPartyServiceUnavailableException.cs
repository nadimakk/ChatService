namespace ChatService.Web.Exceptions;

public class ThirdPartyServiceUnavailableException : Exception
{
    public ThirdPartyServiceUnavailableException(string? message) : base(message)
    {
    }
}