namespace ChatService.Web.Exceptions;

public class UserConversationExistsException : Exception
{
    public UserConversationExistsException(string? message) : base(message)
    {
    }
}