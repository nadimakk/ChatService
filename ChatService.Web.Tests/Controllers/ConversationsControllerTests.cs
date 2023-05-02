using System.Net;
using System.Net.Http.Json;
using ChatService.Web.Dtos;
using ChatService.Web.Enums;
using ChatService.Web.Exceptions;
using ChatService.Web.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json;

namespace ChatService.Web.Tests.Controllers;

public class ConversationsControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _httpClient;
    private readonly Mock<IConversationService> _conversationServiceMock = new();
    
    private static readonly string _username = Guid.NewGuid().ToString();
    private static readonly string _conversationId = Guid.NewGuid().ToString();
    private static readonly long _unixTimeNow = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    private static readonly string _nextContinuationToken = Guid.NewGuid().ToString();
    private readonly string _nonUrlCharactersContinuationToken = "+\"#%&+/?:";

    private GetMessagesParameters _getMessagesParameters = new()
    {
        ConversationId = _conversationId,
        Limit = 50,
        Order = OrderBy.DESC,
        ContinuationToken = null,
        LastSeenMessageTime = 0
    };
    
    private GetConversationsParameters _getConversationsParameters = new()
    {
        Username = _username,
        Limit = 10,
        Order = OrderBy.DESC,
        ContinuationToken = null,
        LastSeenConversationTime = 0
    };

    private static readonly SendMessageRequest _sendMessageRequest = new()
    {
        Id = Guid.NewGuid().ToString(),
        SenderUsername = _username,
        Text = "Hello"
    };
    
    private readonly StartConversationRequest _startConversationRequest = new()
    {
        Participants = new List<string> { _username, Guid.NewGuid().ToString() },
        FirstMessage = _sendMessageRequest
    };
    
    private readonly GetConversationsResult _getConversationsResult = new()
    {
        Conversations = new List<Conversation>{ CreateConversation(), CreateConversation() },
        NextContinuationToken = _nextContinuationToken
    };
    
    public ConversationsControllerTests(WebApplicationFactory<Program> factory)
    {
        _httpClient = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(_conversationServiceMock.Object);
            });
        }).CreateClient();
    }

    [Fact]
    public async Task GetConversations_Success()
    {
        _conversationServiceMock.Setup(m => m.GetConversations(_getConversationsParameters))
            .ReturnsAsync(_getConversationsResult);
        
        string nextUri = "/api/conversations" +
                         $"?username={_username}" +
                         $"&limit={_getConversationsParameters.Limit}" +
                         $"&lastSeenConversationTime={_getConversationsParameters.LastSeenConversationTime}" +
                         $"&continuationToken={WebUtility.UrlEncode(_nextContinuationToken)}";

        var response = await _httpClient.GetAsync($"api/Conversations/?username={_username}");
        var json = await response.Content.ReadAsStringAsync();
        var receivedGetUserConversationsResponse = JsonConvert.DeserializeObject<GetUserConversationsResponse>(json);
        
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(_getConversationsResult.Conversations, receivedGetUserConversationsResponse.Conversations);
        Assert.Equal(nextUri, receivedGetUserConversationsResponse.NextUri);
    }
    
    [Theory]
    [InlineData(null, "1", "1")]
    [InlineData("", "1", "1")]
    [InlineData(" ", "1", "1")]
    [InlineData("foobar", null, "1")]
    [InlineData("foobar", "", "1")]
    [InlineData("foobar", " ", "1")]
    [InlineData("foobar", "NaN", "1")]
    [InlineData("foobar", "1", null)]
    [InlineData("foobar", "1", "")]
    [InlineData("foobar", "1", " ")]
    [InlineData("foobar", "1", "NaN")]
    public async Task GetUserConversations_InvalidQueryParameters(string username, string limit, string lastSeenConversationTime)
    {
        var response = await _httpClient.GetAsync("/api/conversations" +
                                                  $"?username={username}" +
                                                  $"&limit={limit}" +
                                                  $"&lastSeenConversationTime={lastSeenConversationTime}");
        
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    
    [Fact]
    public async Task GetUserConversations_ContinuationTokenEncode()
    {
        _getConversationsParameters.ContinuationToken = null;
        _getConversationsResult.NextContinuationToken = _nonUrlCharactersContinuationToken;
        _conversationServiceMock.Setup(
                m => m.GetConversations(_getConversationsParameters))
            .ReturnsAsync(_getConversationsResult);
        
        var response = await _httpClient.GetAsync("/api/conversations" +
                                                  $"?username={_username}" +
                                                  $"&limit={_getConversationsParameters.Limit}" +
                                                  $"&lastSeenConversationTime={_getConversationsParameters.LastSeenConversationTime}" +
                                                  $"&continuationToken={_getConversationsParameters.ContinuationToken}");
        var json = await response.Content.ReadAsStringAsync();
        var receivedGetUserConversationsResponse = JsonConvert.DeserializeObject<GetUserConversationsResponse>(json);
        
        string expectedEncodedNextUri = "/api/conversations" +
                                       $"?username={_username}" +
                                       $"&limit={_getConversationsParameters.Limit}" +
                                       $"&lastSeenConversationTime={_getConversationsParameters.LastSeenConversationTime}" +
                                       $"&continuationToken={WebUtility.UrlEncode(_getConversationsResult.NextContinuationToken)}";
        
        Assert.Equal(expectedEncodedNextUri, receivedGetUserConversationsResponse.NextUri);
    }
    
    [Fact]
    public async Task GetUserConversations_ContinuationTokenDecode()
    {
        _getConversationsParameters.ContinuationToken = _nonUrlCharactersContinuationToken;
        _conversationServiceMock.Setup(
                m => m.GetConversations(_getConversationsParameters))
            .ReturnsAsync(_getConversationsResult);
        
        var response = await _httpClient.GetAsync("/api/conversations" +
                                                  $"?username={_username}" +
                                                  $"&limit={_getConversationsParameters.Limit}" +
                                                  $"&lastSeenConversationTime={_getConversationsParameters.LastSeenConversationTime}" +
                                                  $"&continuationToken={WebUtility.UrlEncode(_nonUrlCharactersContinuationToken)}");
        
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _conversationServiceMock.Verify( 
            m => m.GetConversations(_getConversationsParameters), Times.Once);
    }
    
    [Fact]
    public async Task GetUserConversations_InvalidArguments()
    {
        _conversationServiceMock.Setup(m => m.GetConversations(_getConversationsParameters))
            .ThrowsAsync(new ArgumentException($"Invalid arguments."));

        var response = await _httpClient.GetAsync($"api/Conversations/?username={_username}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetUserConversations_InvalidContinuationToken()
    {
        string invalidContinuationToken = Guid.NewGuid().ToString();
        _getConversationsParameters.ContinuationToken = invalidContinuationToken;
        
        _conversationServiceMock.Setup(m => m.GetConversations(_getConversationsParameters))
            .ThrowsAsync(new InvalidContinuationTokenException($"Continuation token {invalidContinuationToken} is invalid."));
        
        var response = await _httpClient.GetAsync(
            $"api/Conversations/?username={_username}&continuationToken={invalidContinuationToken}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    
    [Fact]
    public async Task GetUserConversations_UserNotFound()
    {
        _conversationServiceMock.Setup(m => m.GetConversations(_getConversationsParameters))
            .ThrowsAsync(new UserNotFoundException($"A user with the username {_username} was not found."));

        var response = await _httpClient.GetAsync(
            $"api/Conversations/?username={_username}&");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetUserConversations_CosmosServiceUnavailable()
    {
        _conversationServiceMock.Setup(m => m.GetConversations(_getConversationsParameters))
            .ThrowsAsync(new CosmosServiceUnavailableException("Cosmos service is unavailable."));
        
        var response = await _httpClient.GetAsync(
            $"api/Conversations/?username={_username}&");
        
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
    
    [Fact]
    public async Task StartConversation_Success()
    {
        var startConversationServiceResult = new StartConversationResult
        {
            ConversationId = Guid.NewGuid().ToString(),
            CreatedUnixTime = _unixTimeNow
        };

        _conversationServiceMock.Setup(m => m.StartConversation(It.Is<StartConversationRequest>(
                    p => p.Participants.SequenceEqual(_startConversationRequest.Participants) 
                         && p.FirstMessage == _startConversationRequest.FirstMessage)))
            .ReturnsAsync(startConversationServiceResult);

        var expectedStartConversationResponse = new StartConversationResponse
        {
            Id = startConversationServiceResult.ConversationId,
            CreatedUnixTime = startConversationServiceResult.CreatedUnixTime
        };
        
        var response = await _httpClient.PostAsJsonAsync($"api/Conversations/", _startConversationRequest);
        var json = await response.Content.ReadAsStringAsync();
        var receivedStartConversationResponse = JsonConvert.DeserializeObject<StartConversationResponse>(json);
        
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(expectedStartConversationResponse, receivedStartConversationResponse);
    }
    
    [Fact]
    public async Task StartConversation_InvalidArguments()
    {
        _conversationServiceMock.Setup(m => m.StartConversation(It.Is<StartConversationRequest>(
                    p => p.Participants.SequenceEqual(_startConversationRequest.Participants) 
                         && p.FirstMessage == _startConversationRequest.FirstMessage)))
            .ThrowsAsync(new ArgumentException());
        
        var response = await _httpClient.PostAsJsonAsync($"api/Conversations/", _startConversationRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    
    [Fact]
    public async Task StartConversation_ProfileNotFound()
    {
        _conversationServiceMock.Setup(m => m.StartConversation(It.Is<StartConversationRequest>(
                    p => p.Participants.SequenceEqual(_startConversationRequest.Participants) 
                         && p.FirstMessage == _startConversationRequest.FirstMessage)))
            .ThrowsAsync(new UserNotFoundException($"A user with the username {_username} was not found."));
        
        var response = await _httpClient.PostAsJsonAsync($"api/Conversations/", _startConversationRequest);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
    
    [Fact]
    public async Task StartConversation_MessageExists()
    {
        _conversationServiceMock.Setup(m => m.StartConversation(It.Is<StartConversationRequest>(
                    p => p.Participants.SequenceEqual(_startConversationRequest.Participants) 
                         && p.FirstMessage == _startConversationRequest.FirstMessage)))
            .ThrowsAsync(new MessageExistsException(
            $"A message with ID {_startConversationRequest.FirstMessage.Id} already exists."));
        
        var response = await _httpClient.PostAsJsonAsync($"api/Conversations/", _startConversationRequest);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
    
    [Fact]
    public async Task StartConversation_CosmosServiceUnavailable()
    {
        _conversationServiceMock.Setup(m => m.StartConversation(It.Is<StartConversationRequest>(
                p => p.Participants.SequenceEqual(_startConversationRequest.Participants) 
                     && p.FirstMessage == _startConversationRequest.FirstMessage)))
            .ThrowsAsync(new CosmosServiceUnavailableException("Cosmos service is unavailable."));

        var response = await _httpClient.PostAsJsonAsync($"api/Conversations/", _startConversationRequest);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
    
    [Fact]
    public async Task GetMessages_Success()
    {
        List<Message> messages = new();
        messages.Add(new Message
        {
            Id = Guid.NewGuid().ToString(),
            SenderUsername = Guid.NewGuid().ToString(),
            UnixTime = _unixTimeNow
        });
        messages.Add(new Message
        {
            Id = Guid.NewGuid().ToString(),
            SenderUsername = Guid.NewGuid().ToString(),
            UnixTime = _unixTimeNow
        });

        var getMessagesServiceResult = new GetMessagesResult
        {
            Messages = messages,
            NextContinuationToken = _nextContinuationToken
        };
        
        _conversationServiceMock.Setup(m => m.GetMessages(_getMessagesParameters))
            .ReturnsAsync(getMessagesServiceResult);

        string nextUri = $"/api/conversations/{_conversationId}/messages" +
                         $"?limit={_getMessagesParameters.Limit}" +
                         $"&continuationToken={WebUtility.UrlEncode(_nextContinuationToken)}" +
                         $"&lastSeenConversationTime={_getConversationsParameters.LastSeenConversationTime}";

        var response = await _httpClient.GetAsync($"/api/conversations/{_conversationId}/messages/");
        var json = await response.Content.ReadAsStringAsync();
        var receivedGetMessagesResponse = JsonConvert.DeserializeObject<GetMessagesResponse>(json);
        
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(messages, receivedGetMessagesResponse.Messages);
        Assert.Equal(nextUri, receivedGetMessagesResponse.NextUri);
    }
    
    [Theory]
    [InlineData(null, "1")]
    [InlineData("", "1")]
    [InlineData(" ", "1")]
    [InlineData("Nan","1")]
    [InlineData("1", null)]
    [InlineData("1", "")]
    [InlineData("1", " ")]
    [InlineData("1", "NaN")]
    public async Task GetMessages_InvalidQueryParameters(string limit, string lastSeenMessageTime)
    {
        var response = await _httpClient.GetAsync($"/api/conversations/{_conversationId}/messages/" +
                                                  $"?limit={limit}" +
                                                  $"&lastSeenMessageTime={lastSeenMessageTime}");
        
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    
    [Fact]
    public async Task GetMessages_InvalidArguments()
    {
        _conversationServiceMock.Setup(m => m.GetMessages(_getMessagesParameters))
            .ThrowsAsync(new ArgumentException());

        var response = await _httpClient.GetAsync($"/api/conversations/{_conversationId}/messages/");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    
    [Fact]
    public async Task GetMessages_ConversationDoesNotExist()
    {
        _conversationServiceMock.Setup(m => m.GetMessages(_getMessagesParameters))
            .ThrowsAsync(new ConversationDoesNotExistException(
                $"A conversation partition with the conversationId {_conversationId} does not exist."));

        var response = await _httpClient.GetAsync($"/api/conversations/{_conversationId}/messages/");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
    
    [Fact]
    public async Task GetMessages_CosmosServiceUnavailable()
    {
        _conversationServiceMock.Setup(m => m.GetMessages(_getMessagesParameters))
            .ThrowsAsync(new CosmosServiceUnavailableException("Cosmos service is unavailable."));

        var response = await _httpClient.GetAsync($"/api/conversations/{_conversationId}/messages/");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
    
    [Fact]
    public async Task PostMessage_Success()
    {
        var sendMessageResponse = new SendMessageResponse
        {
            CreatedUnixTime = _unixTimeNow
        };

        _conversationServiceMock.Setup(m => m.AddMessage(_conversationId, false, _sendMessageRequest))
            .ReturnsAsync(sendMessageResponse);
        
        var response = await _httpClient.PostAsJsonAsync(
            $"/api/conversations/{_conversationId}/messages/", _sendMessageRequest);
        var json = await response.Content.ReadAsStringAsync();
        var receivedSendMessageResponse = JsonConvert.DeserializeObject<SendMessageResponse>(json);
        
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(sendMessageResponse, receivedSendMessageResponse);
    }
    
    [Fact]
    public async Task PostMessage_InvalidArguments()
    {
        _conversationServiceMock.Setup(m => m.AddMessage(_conversationId, false, _sendMessageRequest))
            .ThrowsAsync(new ArgumentException());
        
        var response = await _httpClient.PostAsJsonAsync(
            $"/api/conversations/{_conversationId}/messages/", _sendMessageRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    
    [Fact]
    public async Task PostMessage_UserNotParticipant()
    {
        _conversationServiceMock.Setup(m => m.AddMessage(_conversationId, false, _sendMessageRequest))
            .ThrowsAsync(new UserNotParticipantException(
                $"User {_username} is not a participant of conversation {_conversationId}."));
        
        var response = await _httpClient.PostAsJsonAsync(
            $"/api/conversations/{_conversationId}/messages/", _sendMessageRequest);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
    
    [Fact]
    public async Task PostMessage_ProfileNotFound()
    {
        _conversationServiceMock.Setup(m => m.AddMessage(_conversationId, false, _sendMessageRequest))
            .ThrowsAsync(new UserNotFoundException(
                $"A user with the username {_username} was not found."));
        
        var response = await _httpClient.PostAsJsonAsync(
            $"/api/conversations/{_conversationId}/messages/", _sendMessageRequest);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
    
    [Fact]
    public async Task PostMessage_ConversationDoesNotExist()
    {
        _conversationServiceMock.Setup(m => m.AddMessage(_conversationId, false, _sendMessageRequest))
            .ThrowsAsync(new ConversationDoesNotExistException(
                $"A conversation partition with the conversationId {_conversationId} does not exist."));
        
        var response = await _httpClient.PostAsJsonAsync(
            $"/api/conversations/{_conversationId}/messages/", _sendMessageRequest);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
    
    [Fact]
    public async Task PostMessage_MessageExists()
    {
        _conversationServiceMock.Setup(m => m.AddMessage(_conversationId, false, _sendMessageRequest))
            .ThrowsAsync(new MessageExistsException($"A message with ID {_sendMessageRequest.Id} already exists."));
        
        var response = await _httpClient.PostAsJsonAsync(
            $"/api/conversations/{_conversationId}/messages/", _sendMessageRequest);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
    
    [Fact]
    public async Task PostMessage_CosmosServiceUnavailable()
    {
        _conversationServiceMock.Setup(m => m.AddMessage(_conversationId, false, _sendMessageRequest))
            .ThrowsAsync(new CosmosServiceUnavailableException("Cosmos service is unavailable."));
        
        var response = await _httpClient.PostAsJsonAsync(
            $"/api/conversations/{_conversationId}/messages/", _sendMessageRequest);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
    
    private static Conversation CreateConversation()
    {
        return new Conversation
        {
            Id = Guid.NewGuid().ToString(),
            LastModifiedUnixTime = _unixTimeNow
        };
    }
}