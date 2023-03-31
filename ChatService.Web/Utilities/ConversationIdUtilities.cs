namespace ChatService.Web.Utilities;

public class ConversationIdUtilities
{
    public static string GenerateConversationId(string username1, string username2)
    {
        if (username1.CompareTo(username2) < 0)
        {
            return username1 + "_" + username2;
        }

        return username2 + "_" + username1;
    }
}