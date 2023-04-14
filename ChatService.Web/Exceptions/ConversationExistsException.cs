namespace ChatService.Web.Exceptions;

public class ConversationExistsException : Exception
{
    public ConversationExistsException(string? message) : base(message)
    {
    }
}