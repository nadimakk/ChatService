namespace ChatService.Web.Exceptions;

public class UserConversationNotFoundException : Exception
{
    public UserConversationNotFoundException(string? message) : base(message)
    {
    }
}