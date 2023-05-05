using ChatService.Web.Dtos;
using ChatService.Web.Enums;
using ChatService.Web.Exceptions;
using ChatService.Web.Services;
using ChatService.Web.Storage;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace ChatService.Web.Tests.Services;

public class ConversationServiceTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly Mock<IMessageStore> _messageStoreMock = new();
    private readonly Mock<IUserConversationStore> _userConversationStoreMock = new();
    private readonly Mock<IProfileService> _profileServiceMock = new();
    private readonly IConversationService _conversationService;
    
    private static readonly long _unixTimeNow = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    private static readonly string _conversationId = Guid.NewGuid().ToString();
    
    private static readonly string _senderUsername = Guid.NewGuid().ToString();
    private static readonly string _recipientUsername = Guid.NewGuid().ToString();
    
    private static readonly List<string> _participants = new()
    {
        _senderUsername,
        _recipientUsername
    };

    private static readonly SendMessageRequest _sendMessageRequest = new()
    {
        Id = Guid.NewGuid().ToString(),
        SenderUsername = _senderUsername,
        Text = Guid.NewGuid().ToString()
    };
    
    private readonly GetConversationsParameters _getConversationsParameters = new()
    {
        Limit = 1,
        Order = OrderBy.ASC,
        ContinuationToken = null,
        LastSeenConversationTime = 0
    };

    private readonly StartConversationRequest _startConversationRequest = new()
    {
        Participants = _participants,
        FirstMessage = _sendMessageRequest
    };
    
    private readonly GetMessagesParameters _getMessagesParameters = new()
    {
        ConversationId = _conversationId,
        Limit = 1,
        Order = OrderBy.ASC,
        ContinuationToken = null,
        LastSeenMessageTime = 0
    };

    public ConversationServiceTests(WebApplicationFactory<Program> factory)
    {
        _conversationService = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(_messageStoreMock.Object);
                services.AddSingleton(_userConversationStoreMock.Object);
                services.AddSingleton(_profileServiceMock.Object);
            });
        }).Services.GetRequiredService<IConversationService>();
    }

    [Fact]
    public async Task CreateConversation_Success()
    {
        string username1 = Guid.NewGuid().ToString();
        string username2 = Guid.NewGuid().ToString();
        string username3 = Guid.NewGuid().ToString();
        
        _profileServiceMock.Setup(m => m.ProfileExists(username1))
            .ReturnsAsync(true);
        _profileServiceMock.Setup(m => m.ProfileExists(username2))
            .ReturnsAsync(true);
        _profileServiceMock.Setup(m => m.ProfileExists(username3))
            .ReturnsAsync(true);

        UpdateStartConversationRequest(senderUsername: username1, recipientUsername: username2);
        var response = await _conversationService.StartConversation(_startConversationRequest);
        string conversationIdUser1User2 = response.ConversationId;
        
        UpdateStartConversationRequest(senderUsername: username2, recipientUsername: username1);
        response = await _conversationService.StartConversation(_startConversationRequest);
        string conversationIdUser2User1 = response.ConversationId;
        
        UpdateStartConversationRequest(senderUsername: username1, recipientUsername: username3);
        response = await _conversationService.StartConversation(_startConversationRequest);
        string conversationIdUser1User3 = response.ConversationId;
        
        Assert.Equal(conversationIdUser1User2, conversationIdUser2User1);
        Assert.NotEqual(conversationIdUser1User2, conversationIdUser1User3);
    }
    
    [Theory]
    [MemberData(nameof(GenerateInvalidParticipantsList))]
    public async Task CreateConversation_InvalidParticipantsList(List<string> participants)
    {
        StartConversationRequest startConversationRequest = new()
        {
            Participants = participants,
            FirstMessage = _sendMessageRequest
        };
        await Assert.ThrowsAsync<ArgumentException>( () => 
            _conversationService.StartConversation(startConversationRequest));
    }

    [Theory]
    [InlineData(null, "senderUsername", "Hello world.")]
    [InlineData("", "senderUsername", "Hello world.")]
    [InlineData(" ", "senderUsername", "Hello world.")]
    [InlineData("messageId", null, "Hello world.")]
    [InlineData("messageId", "", "Hello world.")]
    [InlineData("messageId", " ", "Hello world.")]
    [InlineData("messageId", "senderUsername", null)]
    [InlineData("messageId", "senderUsername", "")]
    [InlineData("messageId", "senderUsername", " ")]
    public async Task CreateConversation_InvalidSendMessageRequest(string messageId, string senderUsername, string text)
    {
        SendMessageRequest sendMessageRequest = new()
        {
            Id = messageId,
            SenderUsername = senderUsername,
            Text = text
        };

        _startConversationRequest.FirstMessage = sendMessageRequest;
        
        await Assert.ThrowsAsync<ArgumentException>( () => 
            _conversationService.StartConversation(_startConversationRequest));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task CreateConversation_ParticipantsNotFound(bool participant1Exists, bool participant2Exists)
    {
        _profileServiceMock.Setup(m => m.ProfileExists(_participants.ElementAt(0)))
            .ReturnsAsync(participant1Exists);
        _profileServiceMock.Setup(m => m.ProfileExists(_participants.ElementAt(1)))
            .ReturnsAsync(participant2Exists);
    
        await Assert.ThrowsAsync<UserNotFoundException>( () => 
            _conversationService.StartConversation(_startConversationRequest));
    }
    
    [Fact]
    public async Task GetUserConversations_Success()
    {
        string username1 = Guid.NewGuid().ToString();
        string username2 = Guid.NewGuid().ToString();
        string username3 = Guid.NewGuid().ToString();
        
        Profile profile2 = CreateProfile(username2);
        Profile profile3 = CreateProfile(username3);

        string conversationIdUser1User2 = await StartConversation(username1, username2);
        string conversationIdUser1User3 = await StartConversation(username1, username3);
        
        List<UserConversation> userConversations = new()
        {
            CreateUserConversation(conversationIdUser1User2, senderUsername: username1, recipientUsername: username2),
            CreateUserConversation(conversationIdUser1User3, senderUsername: username1, recipientUsername: username3)
        };

        string nextContinuationToken = Guid.NewGuid().ToString();
        GetUserConversationsResult result = new()
        {
            UserConversations = userConversations,
            NextContinuationToken = nextContinuationToken
        };
        
        _getConversationsParameters.Username = username1;
        _getConversationsParameters.Limit = 10;
        _getConversationsParameters.Order = OrderBy.DESC;
        
        _userConversationStoreMock.Setup(m => m.GetUserConversations(_getConversationsParameters))
            .ReturnsAsync(result);
        _profileServiceMock.Setup(m => m.GetProfile(username2))
            .ReturnsAsync(profile2);
        _profileServiceMock.Setup(m => m.GetProfile(username3))
            .ReturnsAsync(profile3);

        List<Conversation> conversations = new()
        {
            CreateConversation(conversationIdUser1User2, profile2),
            CreateConversation(conversationIdUser1User3, profile3)
        };
        
        GetConversationsResult expected = new()
        {
            Conversations = conversations,
            NextContinuationToken = nextContinuationToken
        };
        
        var response = await _conversationService.GetConversations(_getConversationsParameters);

        Assert.Equal(expected.Conversations, response.Conversations);
        Assert.Equal(expected.NextContinuationToken, response.NextContinuationToken);
    }

    [Theory]
    [InlineData(null, 10, OrderBy.DESC, null, 0)]
    [InlineData("", 10, OrderBy.DESC, null, 0)]
    [InlineData(" ", 10, OrderBy.DESC, null, 0)]
    [InlineData("username", 0, OrderBy.DESC, null, 0)]
    [InlineData("username", -1, OrderBy.DESC, null, 0)]
    [InlineData("username", 10, OrderBy.DESC, null, -1)]
    public async Task GetUserConversations_InvalidArguments(
        string username, int limit, OrderBy orderBy, string? continuationToken, long lastSeenConversationTime)
    {
        _getConversationsParameters.Username = username;
        _getConversationsParameters.Limit = limit;
        _getConversationsParameters.Order = orderBy;
        _getConversationsParameters.ContinuationToken = continuationToken;
        _getConversationsParameters.LastSeenConversationTime = lastSeenConversationTime;
        await Assert.ThrowsAsync<ArgumentException>( () => _conversationService.GetConversations(_getConversationsParameters));
    }

    [Fact]
    public async Task GetUserConversations_UserNotFound()
    {
        _profileServiceMock.Setup(m => m.ProfileExists(_participants.ElementAt(0)))
            .ReturnsAsync(false);
        
        _getConversationsParameters.Username = _participants.ElementAt(0);
        
        await Assert.ThrowsAsync<UserNotFoundException>( () => 
            _conversationService.GetConversations(_getConversationsParameters));
    }
    
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AddMessage_Success(bool isFirstMessage)
    {
        string conversationId = await StartConversation(_senderUsername, _recipientUsername);

        _messageStoreMock.Setup(m => m.ConversationExists(conversationId))
            .ReturnsAsync(true);

        SendMessageResponse expectedSendMessageResponse = new()
        {
            CreatedUnixTime = _unixTimeNow
        };

        SendMessageResponse receivedSendMessageResponse = await _conversationService.AddMessage(
            conversationId, isFirstMessage, _sendMessageRequest);
        
        receivedSendMessageResponse.CreatedUnixTime = _unixTimeNow;
        
        Assert.Equal(expectedSendMessageResponse, receivedSendMessageResponse);
    }

    [Theory]
    [InlineData(null, "messageId", "senderUsername", "text")]
    [InlineData("", "messageId", "senderUsername", "text")]
    [InlineData(" ", "messageId", "senderUsername", "text")]
    [InlineData("conversationId", null, "senderUsername", "text")]
    [InlineData("conversationId", "", "senderUsername", "text")]
    [InlineData("conversationId", " ", "senderUsername", "text")]
    [InlineData("conversationId", "messageId", null, "text")]
    [InlineData("conversationId", "messageId", "", "text")]
    [InlineData("conversationId", "messageId", " ", "text")]
    [InlineData("conversationId", "messageId", "senderUsername", null)]
    [InlineData("conversationId", "messageId", "senderUsername", "")]
    [InlineData("conversationId", "messageId", "senderUsername", " ")]
    public async Task AddMessage_InvalidArguments(
        string conversationId, string messageId, string senderUsername, string text)
    {
        SendMessageRequest sendMessageRequest = new()
        {
            Id = messageId,
            SenderUsername = senderUsername,
            Text = text
        };

        await Assert.ThrowsAsync<ArgumentException>(() => _conversationService.AddMessage(
            conversationId, true, sendMessageRequest));
    }

    [Fact]
    public async Task AddMessage_UserNotParticipant()
    {
        
        string conversationId = await StartConversation(_senderUsername, _recipientUsername);
        
        _sendMessageRequest.SenderUsername = Guid.NewGuid().ToString();
        
        _messageStoreMock.Setup(m => m.ConversationExists(conversationId))
            .ReturnsAsync(true);
        
        _profileServiceMock.Setup(m => m.ProfileExists(_sendMessageRequest.SenderUsername))
            .ReturnsAsync(true);
        
        await Assert.ThrowsAsync<UserNotParticipantException>(
            () => _conversationService.AddMessage(conversationId, true, _sendMessageRequest));
    }

    [Fact]
    public async Task AddMessage_ProfileNotFound()
    {
        _profileServiceMock.Setup(m => m.ProfileExists(_senderUsername))
            .ReturnsAsync(false);
        
        await Assert.ThrowsAsync<UserNotFoundException>(() => _conversationService.AddMessage(
            _conversationId, true, _sendMessageRequest));
    }
    
    [Fact]
    public async Task AddMessage_ConversationDoesNotExist()
    {
        _profileServiceMock.Setup(m => m.ProfileExists(_senderUsername))
            .ReturnsAsync(true);
    
        _messageStoreMock.Setup(m => m.ConversationExists(_conversationId))
            .ReturnsAsync(false);
        
        await Assert.ThrowsAsync<ConversationDoesNotExistException>(() => _conversationService.AddMessage(
            _conversationId, false, _sendMessageRequest));
    }

    [Fact]
    public async Task GetMessages_Success()
    {
        _messageStoreMock.Setup(m => m.ConversationExists(_conversationId))
            .ReturnsAsync(true);

        List<Message> messages = new() { CreateMessage(), CreateMessage() };

        string nextContinuationToken = Guid.NewGuid().ToString();
        
        GetMessagesResult getMessagesResult = new()
        {
            Messages = messages,
            NextContinuationToken = nextContinuationToken
        };
        
        _messageStoreMock.Setup(m => m.GetMessages(_conversationId, _getMessagesParameters))
            .ReturnsAsync(getMessagesResult);

        GetMessagesResult expectedGetMessagesResult = new()
        {
            Messages = messages,
            NextContinuationToken = nextContinuationToken
        };

        _getMessagesParameters.Limit = 10;
        _getMessagesParameters.Order = OrderBy.DESC;
        GetMessagesResult receivedGetMessagesResult = await _conversationService.GetMessages(_getMessagesParameters);
        
        Assert.Equal(expectedGetMessagesResult.Messages, receivedGetMessagesResult.Messages);
        Assert.Equal(expectedGetMessagesResult.NextContinuationToken, receivedGetMessagesResult.NextContinuationToken);
    }

    [Theory]
    [InlineData(null, 1, 1)]
    [InlineData("", 1, 1)]
    [InlineData(" ", 1, 1)]
    [InlineData("conversationId", 0, 1)]
    [InlineData("conversationId", -1, 1)]
    [InlineData("conversationId", 1, -1)]
    public async Task GetMessages_InvalidArguments(string conversationId, int limit, long lastSeenConversationTime)
    {
        _getMessagesParameters.ConversationId = conversationId;
        _getMessagesParameters.Limit = limit;
        _getMessagesParameters.LastSeenMessageTime = lastSeenConversationTime;
        
        await Assert.ThrowsAsync<ArgumentException>(() => _conversationService.GetMessages(_getMessagesParameters));
    }
    
    [Fact]
    public async Task GetMessages_ConversationDoesNotExist()
    {
        _messageStoreMock.Setup(m => m.ConversationExists(_conversationId))
            .ReturnsAsync(false);
        
        await Assert.ThrowsAsync<ConversationDoesNotExistException>(
            () => _conversationService.GetMessages(_getMessagesParameters));
    }

    public static IEnumerable<object[]> GenerateInvalidParticipantsList(){
        
        yield return new object[] { new List<string> {_participants.ElementAt(0), ""} };
        yield return new object[] { new List<string> {_participants.ElementAt(0), " "} };
        
        yield return new object[] { new List<string> { "", _participants.ElementAt(1) } };
        yield return new object[] { new List<string> { " ", _participants.ElementAt(1) } };

        yield return new object[] { new List<string>
        {
            _participants.ElementAt(0), 
            _participants.ElementAt(0)
        } };
        
        yield return new object[] { new List<string> { _participants.ElementAt(0) } };
    }

    private async Task<string> StartConversation(string username1, string username2)
    {
        _profileServiceMock.Setup(m => m.ProfileExists(username1))
            .ReturnsAsync(true);
        _profileServiceMock.Setup(m => m.ProfileExists(username2))
            .ReturnsAsync(true);
        
        _startConversationRequest.Participants = new List<string>
        {
            username1,
            username2
        };

        _sendMessageRequest.SenderUsername = username1;
    
        StartConversationResult result = await _conversationService.StartConversation(_startConversationRequest);
        return result.ConversationId;
    }
    
    private Message CreateMessage()
    {
        return new Message()
        {
            Id = Guid.NewGuid().ToString(),
            UnixTime = _unixTimeNow,
            SenderUsername = _senderUsername,
            Text = Guid.NewGuid().ToString()
        };
    }
    
    private Conversation CreateConversation(string conversationId, Profile recipientProfile)
    {
        return new Conversation
        {
            Id = conversationId,
            LastModifiedUnixTime = _unixTimeNow,
            Recipient = recipientProfile
        };
    }
    
    private UserConversation CreateUserConversation(string conversationId, string senderUsername, 
        string recipientUsername)
    {
        return new UserConversation
        {
            Username = senderUsername,
            ConversationId = conversationId,
            OtherParticipantUsername = recipientUsername,
            LastModifiedTime = _unixTimeNow
        };
    }
    
    private Profile CreateProfile(string username)
    {
        return new Profile
        {
            Username = username,
            FirstName = Guid.NewGuid().ToString(),
            LastName = Guid.NewGuid().ToString(),
            ProfilePictureId = Guid.NewGuid().ToString()
        };
    }

    private void UpdateStartConversationRequest(string senderUsername, string recipientUsername)
    {
        _startConversationRequest.Participants = new List<string> { senderUsername, recipientUsername };
        _startConversationRequest.FirstMessage.SenderUsername = senderUsername;
    }
}