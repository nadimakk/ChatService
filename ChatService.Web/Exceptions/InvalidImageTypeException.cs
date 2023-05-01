namespace ChatService.Web.Exceptions;

public class InvalidImageTypeException : Exception
{
    public InvalidImageTypeException(string? message) : base(message)
    {
    }
}