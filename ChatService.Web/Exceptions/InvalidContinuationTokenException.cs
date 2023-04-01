namespace ChatService.Web.Exceptions;

public class InvalidContinuationTokenException  : Exception
{
    public InvalidContinuationTokenException(string? message) : base(message)
    {
    }
}