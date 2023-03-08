namespace ChatService.Web.Exceptions;

public class ImageNotFoundException : Exception
{
    public ImageNotFoundException(string? message) : base(message)
    {
    }
}