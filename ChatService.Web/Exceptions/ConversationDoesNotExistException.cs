namespace ChatService.Web.Exceptions;

public class ConversationDoesNotExistException : Exception
{
    public ConversationDoesNotExistException(string? message) : base(message)
    {
    }
}