using ChatService.Web.Exceptions;

namespace ChatService.Web.Utilities;

public class ConversationIdUtilities
{
    private static readonly char Seperator = '_';
    
    public static string GenerateConversationId(string username1, string username2)
    {
        if (username1.CompareTo(username2) < 0)
        {
            return username1 + Seperator + username2;
        }
        return username2 + Seperator + username1;
    }

    public static string[] SplitConversationId(string conversationId)
    {
        return conversationId.Split(Seperator);
    }

    public static void ValidateConversationId(string conversationId)
    {
        if (string.IsNullOrWhiteSpace(conversationId) || !conversationId.Contains(Seperator))
        {
            throw new ArgumentException($"Invalid conversationId {conversationId}.");
        }
    }

    public static void ValidateUsernameSeparator(string username)
    {
        if (username.Contains(Seperator))
        {
            throw new InvalidUsernameException($"Username {username} is invalid. Usernames cannot have an underscore.");
        }
    }
}