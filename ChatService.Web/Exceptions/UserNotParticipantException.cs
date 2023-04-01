namespace ChatService.Web.Exceptions;

public class UserNotParticipantException : Exception
{
    public UserNotParticipantException(string? message) : base(message)
    {
    }
}