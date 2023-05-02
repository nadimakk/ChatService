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

public class UserConversationServiceTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly Mock<IUserConversationStore> _userConversationStoreMock = new();
    private readonly Mock<IProfileService> _profileServiceMock = new();
    
    private const char Separator = '_';
    
    private readonly IConversationService _conversationService;

    private static readonly long _unixTimeNow = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    private static readonly List<string> _participants = new()
    {
        Guid.NewGuid().ToString(),
        Guid.NewGuid().ToString()
    };

    private static readonly SendMessageRequest _sendMessageRequest = new()
    {
        Id = Guid.NewGuid().ToString(),
        SenderUsername = _participants.ElementAt(0),
        Text = Guid.NewGuid().ToString()
    };
    
    private GetConversationsParameters _parameters = new()
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

    public UserConversationServiceTests(WebApplicationFactory<Program> factory)
    {
        _conversationService = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(_userConversationStoreMock.Object);
                services.AddSingleton(_profileServiceMock.Object);
            });
        }).Services.GetRequiredService<IConversationService>();
    }

    [Fact]
    public async Task CreateConversation_Success()
    {
        _profileServiceMock.Setup(m => m.ProfileExists(_participants.ElementAt(0)))
            .ReturnsAsync(true);
        _profileServiceMock.Setup(m => m.ProfileExists(_participants.ElementAt(1)))
            .ReturnsAsync(true);

        var response = await _conversationService.StartConversation(_startConversationRequest);

        response.CreatedUnixTime = _unixTimeNow;

        StartConversationResult expected = new()
        {
            ConversationId = GenerateConversationId(
                _participants.ElementAt(0), _participants.ElementAt(1)),
            CreatedUnixTime = _unixTimeNow
        };

        Assert.Equal(expected, response);
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
        _profileServiceMock.Setup(m => m.ProfileExists(username1))
            .ReturnsAsync(true);
        
        string username2 = Guid.NewGuid().ToString();
        string username3 = Guid.NewGuid().ToString();
        Profile profile2 = CreateProfile(username2);
        Profile profile3 = CreateProfile(username3);

        List<UserConversation> userConversations = new()
        {
            CreateUserConversation(senderUsername: username1, recipientUsername: username2),
            CreateUserConversation(senderUsername: username1, recipientUsername: username3)
        };

        string nextContinuationToken = Guid.NewGuid().ToString();
        GetUserConversationsResult result = new()
        {
            UserConversations = userConversations,
            NextContinuationToken = nextContinuationToken
        };
        
        _parameters.Username = username1;
        _parameters.Limit = 10;
        _parameters.Order = OrderBy.DESC;
        
        _userConversationStoreMock.Setup(m => m.GetUserConversations(_parameters))
            .ReturnsAsync(result);
        _profileServiceMock.Setup(m => m.GetProfile(username2))
            .ReturnsAsync(profile2);
        _profileServiceMock.Setup(m => m.GetProfile(username3))
            .ReturnsAsync(profile3);

        List<Conversation> conversations = new()
        {
            CreateConversation(senderUsername: username1, recipientProfile: profile2),
            CreateConversation(senderUsername: username1, recipientProfile: profile3)
        };
        
        GetConversationsResult expected = new()
        {
            Conversations = conversations,
            NextContinuationToken = nextContinuationToken
        };
        
        var response = await _conversationService.GetConversations(_parameters);

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
        _parameters.Username = username;
        _parameters.Limit = limit;
        _parameters.Order = orderBy;
        _parameters.ContinuationToken = continuationToken;
        _parameters.LastSeenConversationTime = lastSeenConversationTime;
        await Assert.ThrowsAsync<ArgumentException>( () => _conversationService.GetConversations(_parameters));
    }

    [Fact]
    public async Task GetUserConversations_UserNotFound()
    {
        _profileServiceMock.Setup(m => m.ProfileExists(_participants.ElementAt(0)))
            .ReturnsAsync(false);
        
        _parameters.Username = _participants.ElementAt(0);
        
        await Assert.ThrowsAsync<UserNotFoundException>( () => 
            _conversationService.GetConversations(_parameters));
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
    
    private Conversation CreateConversation(string senderUsername, Profile recipientProfile)
    {
        return new Conversation
        {
            Id = GenerateConversationId(senderUsername, recipientProfile.Username),
            LastModifiedUnixTime = _unixTimeNow,
            Recipient = recipientProfile
        };
    }
    
    private UserConversation CreateUserConversation(string senderUsername, string recipientUsername)
    {
        return new UserConversation
        {
            Username = senderUsername,
            ConversationId = GenerateConversationId(senderUsername, recipientUsername),
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

    private static string GenerateConversationId(string username1, string username2)
    {
        if (username1.CompareTo(username2) < 0)
        {
            return username1 + Separator + username2;
        }
        return username2 + Separator + username1;
    }
}