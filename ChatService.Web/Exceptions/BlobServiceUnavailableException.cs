namespace ChatService.Web.Exceptions;

public class BlobServiceUnavailableException : Exception
{
    public BlobServiceUnavailableException(string? message) : base(message)
    {
    }
}