namespace ChatService.Web.Exceptions;

public class MessageExistsException : Exception
{
    public MessageExistsException(string? message) : base(message)
    {
    }
}