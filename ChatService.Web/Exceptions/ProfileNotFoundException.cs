namespace ChatService.Web.Exceptions;

public class ProfileNotFoundException : Exception
{
    public ProfileNotFoundException(string? message) : base(message)
    {
    }
}