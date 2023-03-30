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
    private readonly Mock<IMessageService> _messageServiceMock = new();
    private readonly Mock<IUserConversationStore> _userConversationStoreMock = new();
    private readonly Mock<IProfileService> _profileServiceMock = new();

    private readonly IUserConversationService _userConversationService;

    private static readonly long _unixTimeNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private static readonly List<string> _participants = new List<string>
    {
        Guid.NewGuid().ToString(),
        Guid.NewGuid().ToString()
    };

    private static readonly SendMessageRequest _sendMessageRequest = new SendMessageRequest
    {
        MessageId = Guid.NewGuid().ToString(),
        SenderUsername = _participants.ElementAt(0),
        Text = "Hello World."
    };

    private readonly StartConversationRequest _startConversationRequest = new StartConversationRequest
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

        StartConversationResponse expected = new StartConversationResponse
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
        StartConversationRequest startConversationRequest = new StartConversationRequest
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
        SendMessageRequest sendMessageRequest = new SendMessageRequest
        {
            MessageId = messageId,
            SenderUsername = senderUsername,
            Text = text
        };

        _startConversationRequest.FirstMessage = sendMessageRequest;
        
        await Assert.ThrowsAsync<ArgumentException>( () => 
            _userConversationService.CreateConversation(_startConversationRequest));
    }

    [Fact]
    public async Task CreateConversation_Profile1NotFound()
    {
        _profileServiceMock.Setup(m => m.ProfileExists(_participants.ElementAt(0)))
            .ReturnsAsync(false);

        _profileServiceMock.Setup(m => m.ProfileExists(_participants.ElementAt(1)))
            .ReturnsAsync(true);

        await Assert.ThrowsAsync<ProfileNotFoundException>( () => 
            _userConversationService.CreateConversation(_startConversationRequest));
    }

    [Fact]
    public async Task CreateConversation_Profile2NotFound()
    {
        _profileServiceMock.Setup(m => m.ProfileExists(_participants.ElementAt(0)))
            .ReturnsAsync(true);

        _profileServiceMock.Setup(m => m.ProfileExists(_participants.ElementAt(1)))
            .ReturnsAsync(false);

        await Assert.ThrowsAsync<ProfileNotFoundException>( () => 
            _userConversationService.CreateConversation(_startConversationRequest));
    }

    [Fact]
    public async Task GetUserConversations_Success()
    {
        string username1 = Guid.NewGuid().ToString();
        string username2 = Guid.NewGuid().ToString();
        string username3 = Guid.NewGuid().ToString();
        
        _profileServiceMock.Setup(m => m.ProfileExists(username1))
            .ReturnsAsync(true);

        Profile profile2 = new Profile
        {
            Username = username2,
            FirstName = Guid.NewGuid().ToString(),
            LastName = Guid.NewGuid().ToString(),
            ProfilePictureId = Guid.NewGuid().ToString()
        };
        Profile profile3 = new Profile
        {
            Username = username3,
            FirstName = Guid.NewGuid().ToString(),
            LastName = Guid.NewGuid().ToString(),
            ProfilePictureId = Guid.NewGuid().ToString()
        };
        
        List<UserConversation> userConversations = new List<UserConversation>
        {
            new UserConversation
            {
                Username = username1,
                ConversationId = GenerateConversationId(username1, username2),
                LastModifiedTime = _unixTimeNow
            },
            new UserConversation
            {
                Username = username1,
                ConversationId = GenerateConversationId(username1, username3),
                LastModifiedTime = _unixTimeNow
            }
        };

        string nextContinuationToken = Guid.NewGuid().ToString();
        
        _userConversationStoreMock.Setup(m => m.GetUserConversations(
                username1, 10, OrderBy.DESC, null, 0))
            .ReturnsAsync((userConversations, nextContinuationToken));
        
        _profileServiceMock.Setup(m => m.GetProfile(username2))
            .ReturnsAsync(profile2);
        
        _profileServiceMock.Setup(m => m.GetProfile(username3))
            .ReturnsAsync(profile3);

            List<Conversation> conversations = new List<Conversation>
            {
                new Conversation
                {  
                    ConversationId = GenerateConversationId(username1, username2),
                    LastModifiedUnixTime = _unixTimeNow,
                    Recipient = profile2
                },
                new Conversation
                {
                    ConversationId = GenerateConversationId(username1, username3),
                    LastModifiedUnixTime = _unixTimeNow,
                    Recipient = profile3
                }
            };
            
            GetUserConversationsServiceResult expected = new GetUserConversationsServiceResult
            {
                Conversations = conversations,
                NextContinuationToken = nextContinuationToken
            };
            
        var response = await _userConversationService.GetUserConversations(
            username1, 10, OrderBy.DESC, null, 0);

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
        await Assert.ThrowsAsync<ArgumentException>( () => 
            _userConversationService.GetUserConversations(
                username, limit, orderBy, continuationToken, lastSeenConversationTime));
    }

    [Fact]
    public async Task GetUserConversations_UserNotFound()
    {
        _profileServiceMock.Setup(m => m.ProfileExists(_participants.ElementAt(0)))
            .ReturnsAsync(false);
        
        await Assert.ThrowsAsync<UserNotFoundException>( () => 
            _userConversationService.GetUserConversations(
                _participants.ElementAt(0), 10, OrderBy.DESC, null, 0));
    }
    
    private string GenerateConversationId(string username1, string username2)
    {
        if (username1.CompareTo(username2) < 0)
        {
            return username1 + "_" + username2;
        }
        return username2 + "_" + username1;
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
}