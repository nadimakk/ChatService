using ChatService.Web.Dtos;
using ChatService.Web.Enums;
using ChatService.Web.Storage;

namespace ChatService.Web.Services;

public class ConversationService : IConversationService
{

    private readonly IMessageStore _messageStore;
    private readonly IConversationStore _conversationStore;

    public ConversationService(IMessageStore messageStore, IConversationStore conversationStore)
    {
        _messageStore = messageStore;
        _conversationStore = conversationStore;
    }

    public async Task<StartConversationResponse> CreateConversation(StartConversationRequest request)
    {
        //TODO:
        //////SPLIT THIS INTO MULTIPLE IFS TO THROW A DIFFERENT ARGUMENT EXCEPTION FOR EACH
        if (request == null ||
            request.participants.Count < 2 ||
            //TODO:
            string.IsNullOrEmpty(request.participants.ElementAt(0)) || //WRITE A TEST TO SEE THE PYTHON THING!!!!!!!!!!!!!!
            string.IsNullOrEmpty(request.participants.ElementAt(1)) ||
            request.participants.ElementAt(0).Equals(request.participants.ElementAt(1)) ||
            string.IsNullOrEmpty(request.firstMessage.id) ||
            string.IsNullOrEmpty(request.firstMessage.SenderUsername) ||
            string.IsNullOrEmpty(request.firstMessage.text)
           )
        {
            throw new ArgumentException($"Invalid StartConversationRequest {request}.");
        }
        
        string conversationId;
        string username1 = request.participants.ElementAt(0);
        string username2 = request.participants.ElementAt(1);
        
        if (username1.CompareTo(username2) < 0)
        {
            conversationId = username1 + "_" + username2;
        }
        else
        {
            conversationId = username2 + "_" + username1;
        }

        long unixTimeNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        
        //TODO:
        ////////////// MOVE THIS TO MESSAGE SERVICE and call it from here
        Message message = new Message
        {
            id = request.firstMessage.id,
            unixTime = unixTimeNow,
            senderUsername = request.firstMessage.SenderUsername,
            text = request.firstMessage.text
        };
        await _messageStore.AddMessage(conversationId, message);
        //////////////////////////////////////////////////////////
        
        UserConversation userConversation1 = new UserConversation
        {
            username = username1,
            conversationId = conversationId,
            lastModifiedTime = unixTimeNow
        };
        await _conversationStore.CreateUserConversation(userConversation1);
        
        UserConversation userConversation2 = new UserConversation
        {
            username = username2,
            conversationId = conversationId,
            lastModifiedTime = unixTimeNow
        };
        await _conversationStore.CreateUserConversation(userConversation2);

        return new StartConversationResponse
        {
            Id = conversationId,
            CreatedUnixTime = unixTimeNow
        };
    }

    public Task<GetConversationsResponse> GetConversations(
        string username, string limit, OrderBy orderBy, string? continuationToken, string lastSeenConversationTime)
    {
        throw new NotImplementedException();
    }
}