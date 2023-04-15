namespace ChatService.Web.Exceptions;

public class InvalidUsernameException : Exception
{
    public InvalidUsernameException(string? message) : base(message)
    {
    }
}