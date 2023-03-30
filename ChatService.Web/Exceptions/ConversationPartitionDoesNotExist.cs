namespace ChatService.Web.Exceptions;

public class ConversationPartitionDoesNotExist : Exception
{
    public ConversationPartitionDoesNotExist(string? message) : base(message)
    {
    }
}