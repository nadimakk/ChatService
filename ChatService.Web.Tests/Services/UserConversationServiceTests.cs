using ChatService.Web.Dtos;
using ChatService.Web.Enums;
using ChatService.Web.Exceptions;
using ChatService.Web.Services;
using ChatService.Web.Storage;
using ChatService.Web.Utilities;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace ChatService.Web.Tests.Services;

public class UserConversationServiceTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly Mock<IMessageService> _messageServiceMock = new();
    private readonly Mock<IUserConversationStore> _userConversationStoreMock = new();
    private readonly Mock<IProfileService> _profileServiceMock = new();

    private readonly IUserConversationService _userConversationService;

    private static readonly long _unixTimeNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

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
    
    private GetUserConversationsParameters _parameters = new()
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
        _userConversationService = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(_messageServiceMock.Object);
                services.AddSingleton(_userConversationStoreMock.Object);
                services.AddSingleton(_profileServiceMock.Object);
            });
        }).Services.GetRequiredService<IUserConversationService>();
    }

    [Fact]
    public async Task CreateConversation_Success()
    {
        _profileServiceMock.Setup(m => m.ProfileExists(_participants.ElementAt(0)))
            .ReturnsAsync(true);
        _profileServiceMock.Setup(m => m.ProfileExists(_participants.ElementAt(1)))
            .ReturnsAsync(true);

        var response = await _userConversationService.CreateConversation(_startConversationRequest);

        response.CreatedUnixTime = _unixTimeNow;

        StartConversationResult expected = new()
        {
            ConversationId = ConversationIdUtilities.GenerateConversationId(
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
            _userConversationService.CreateConversation(startConversationRequest));
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
            _userConversationService.CreateConversation(_startConversationRequest));
    }

    // [Fact]
    // public async Task CreateConversation_Participant1NotFound()
    // {
    //     _profileServiceMock.Setup(m => m.ProfileExists(_participants.ElementAt(0)))
    //         .ReturnsAsync(false);
    //     _profileServiceMock.Setup(m => m.ProfileExists(_participants.ElementAt(1)))
    //         .ReturnsAsync(true);
    //
    //     await Assert.ThrowsAsync<UserNotFoundException>( () => 
    //         _userConversationService.CreateConversation(_startConversationRequest));
    // }

    // [Fact]
    // public async Task CreateConversation_Participant2NotFound()
    // {
    //     _profileServiceMock.Setup(m => m.ProfileExists(_participants.ElementAt(0)))
    //         .ReturnsAsync(true);
    //     _profileServiceMock.Setup(m => m.ProfileExists(_participants.ElementAt(1)))
    //         .ReturnsAsync(false);
    //     
    //     await Assert.ThrowsAsync<UserNotFoundException>( () => 
    //         _userConversationService.CreateConversation(_startConversationRequest));
    // }

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
        
        GetUserConversationsParameters parameters = new()
        {
            Limit = 10,
            Order = OrderBy.DESC,
            ContinuationToken = null,
            LastSeenConversationTime = 0
        };
        
        string nextContinuationToken = Guid.NewGuid().ToString();
        GetUserConversationsResult result = new()
        {
            UserConversations = userConversations,
            NextContinuationToken = nextContinuationToken
        };
        
        _userConversationStoreMock.Setup(m => m.GetUserConversations(username1, parameters))
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

        _parameters.Limit = 10;
        _parameters.Order = OrderBy.DESC;
        var response = await _userConversationService.GetUserConversations(username1, _parameters);

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
        _parameters.Limit = limit;
        _parameters.Order = orderBy;
        _parameters.ContinuationToken = continuationToken;
        _parameters.LastSeenConversationTime = lastSeenConversationTime;
        await Assert.ThrowsAsync<ArgumentException>( () => 
            _userConversationService.GetUserConversations(username, _parameters));
    }

    // [Fact]
    // public async Task GetUserConversations_UserNotFound()
    // {
    //     _profileServiceMock.Setup(m => m.ProfileExists(_participants.ElementAt(0)))
    //         .ReturnsAsync(false);
    //     
    //     await Assert.ThrowsAsync<UserNotFoundException>( () => 
    //         _userConversationService.GetUserConversations(_participants.ElementAt(0), _parameters));
    // }
    
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
            Id = ConversationIdUtilities.GenerateConversationId(senderUsername, recipientProfile.Username),
            LastModifiedUnixTime = _unixTimeNow,
            Recipient = recipientProfile
        };
    }
    
    private UserConversation CreateUserConversation(string senderUsername, string recipientUsername)
    {
        return new UserConversation
        {
            Username = senderUsername,
            ConversationId = ConversationIdUtilities.GenerateConversationId(senderUsername, recipientUsername),
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
}