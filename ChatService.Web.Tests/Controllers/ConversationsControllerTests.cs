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
    private readonly Mock<IUserConversationService> _userConversationServiceMock = new();
    private readonly Mock<IMessageService> _messageServiceMock = new();
    
    private static readonly string _username = Guid.NewGuid().ToString();
    private readonly string _conversationId = Guid.NewGuid().ToString();
    private readonly static long _unixTimeNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    private readonly static string _nextContinuationToken = Guid.NewGuid().ToString();
    private readonly string _nonUrlCharactersContinuationToken = "+\"#%&+/?:";

    private GetMessagesParameters _getMessagesParameters = new()
    {
        Limit = 50,
        Order = OrderBy.DESC,
        ContinuationToken = null,
        LastSeenMessageTime = 0
    };
    
    private GetUserConversationsParameters _getUserConversationsParameters = new()
    {
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
                services.AddSingleton(_userConversationServiceMock.Object);
                services.AddSingleton(_messageServiceMock.Object);
            });
        }).CreateClient();
    }

    [Fact]
    public async Task GetUserConversations_Success()
    {
        _userConversationServiceMock.Setup(m => m.GetUserConversations(_username, _getUserConversationsParameters))
            .ReturnsAsync(_getConversationsResult);
        
        string nextUri = "/api/conversations" +
                         $"?username={_username}" +
                         $"&limit={_getUserConversationsParameters.Limit}" +
                         $"&lastSeenConversationTime={_getUserConversationsParameters.LastSeenConversationTime}" +
                         $"&continuationToken={WebUtility.UrlEncode(_nextContinuationToken)}";

        var response = await _httpClient.GetAsync($"api/Conversations/?username={_username}");
        var json = await response.Content.ReadAsStringAsync();
        var receivedGetUserConversationsResponse = JsonConvert.DeserializeObject<GetUserConversationsResponse>(json);
        
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(_getConversationsResult.Conversations, receivedGetUserConversationsResponse.Conversations);
        Assert.Equal(nextUri, receivedGetUserConversationsResponse.NextUri);
    }

    [Fact]
    public async Task GetUserConversations_ContinuationTokenEncode()
    {
        _getUserConversationsParameters.ContinuationToken = null;
        _getConversationsResult.NextContinuationToken = _nonUrlCharactersContinuationToken;
        _userConversationServiceMock.Setup(
                m => m.GetUserConversations(_username, _getUserConversationsParameters))
            .ReturnsAsync(_getConversationsResult);
        
        var response = await _httpClient.GetAsync("/api/conversations" +
                                                  $"?username={_username}" +
                                                  $"&limit={_getUserConversationsParameters.Limit}" +
                                                  $"&lastSeenConversationTime={_getUserConversationsParameters.LastSeenConversationTime}" +
                                                  $"&continuationToken={_getUserConversationsParameters.ContinuationToken}");
        var json = await response.Content.ReadAsStringAsync();
        var receivedGetUserConversationsResponse = JsonConvert.DeserializeObject<GetUserConversationsResponse>(json);
        
        string expectedEncodedNextUri = "/api/conversations" +
                                       $"?username={_username}" +
                                       $"&limit={_getUserConversationsParameters.Limit}" +
                                       $"&lastSeenConversationTime={_getUserConversationsParameters.LastSeenConversationTime}" +
                                       $"&continuationToken={WebUtility.UrlEncode(_getConversationsResult.NextContinuationToken)}";
        
        Assert.Equal(expectedEncodedNextUri, receivedGetUserConversationsResponse.NextUri);
    }
    
    [Fact]
    public async Task GetUserConversations_ContinuationTokenDecode()
    {
        _getUserConversationsParameters.ContinuationToken = _nonUrlCharactersContinuationToken;
        _userConversationServiceMock.Setup(
                m => m.GetUserConversations(_username, _getUserConversationsParameters))
            .ReturnsAsync(_getConversationsResult);
        
        var response = await _httpClient.GetAsync("/api/conversations" +
                                                  $"?username={_username}" +
                                                  $"&limit={_getUserConversationsParameters.Limit}" +
                                                  $"&lastSeenConversationTime={_getUserConversationsParameters.LastSeenConversationTime}" +
                                                  $"&continuationToken={WebUtility.UrlEncode(_nonUrlCharactersContinuationToken)}");
        
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _userConversationServiceMock.Verify( 
            m => m.GetUserConversations(_username, _getUserConversationsParameters), Times.Once);
    }
    
    [Fact]
    public async Task GetUserConversations_InvalidArguments()
    {
        _userConversationServiceMock.Setup(m => m.GetUserConversations(_username, _getUserConversationsParameters))
            .ThrowsAsync(new ArgumentException($"Invalid arguments."));

        var response = await _httpClient.GetAsync($"api/Conversations/?username={_username}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetUserConversations_InvalidContinuationToken()
    {
        string invalidContinuationToken = Guid.NewGuid().ToString();
        _getUserConversationsParameters.ContinuationToken = invalidContinuationToken;
        
        _userConversationServiceMock.Setup(m => m.GetUserConversations(
                _username, _getUserConversationsParameters))
            .ThrowsAsync(new InvalidContinuationTokenException($"Continuation token {invalidContinuationToken} is invalid."));
        
        var response = await _httpClient.GetAsync(
            $"api/Conversations/?username={_username}&continuationToken={invalidContinuationToken}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    
    [Fact]
    public async Task GetUserConversations_UserNotFound()
    {
        _userConversationServiceMock.Setup(m => m.GetUserConversations(
                _username, _getUserConversationsParameters))
            .ThrowsAsync(new UserNotFoundException($"A user with the username {_username} was not found."));

        var response = await _httpClient.GetAsync(
            $"api/Conversations/?username={_username}&");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetUserConversations_ThirdPartyServiceUnavailable()
    {
        _userConversationServiceMock.Setup(m => m.GetUserConversations(
                _username, _getUserConversationsParameters))
            .ThrowsAsync(new ThirdPartyServiceUnavailableException("Third party service is unavailable."));
        
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

        _userConversationServiceMock.Setup(m => m.CreateConversation(It.Is<StartConversationRequest>(
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
        _userConversationServiceMock.Setup(m => m.CreateConversation(It.Is<StartConversationRequest>(
                    p => p.Participants.SequenceEqual(_startConversationRequest.Participants) 
                         && p.FirstMessage == _startConversationRequest.FirstMessage)))
            .ThrowsAsync(new ArgumentException());
        
        var response = await _httpClient.PostAsJsonAsync($"api/Conversations/", _startConversationRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    
    [Fact]
    public async Task StartConversation_ProfileNotFound()
    {
        _userConversationServiceMock.Setup(m => m.CreateConversation(It.Is<StartConversationRequest>(
                    p => p.Participants.SequenceEqual(_startConversationRequest.Participants) 
                         && p.FirstMessage == _startConversationRequest.FirstMessage)))
            .ThrowsAsync(new UserNotFoundException($"A user with the username {_username} was not found."));
        
        var response = await _httpClient.PostAsJsonAsync($"api/Conversations/", _startConversationRequest);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
    
    [Fact]
    public async Task StartConversation_MessageExists()
    {
        _userConversationServiceMock.Setup(m => m.CreateConversation(It.Is<StartConversationRequest>(
                    p => p.Participants.SequenceEqual(_startConversationRequest.Participants) 
                         && p.FirstMessage == _startConversationRequest.FirstMessage)))
            .ThrowsAsync(new MessageExistsException(
            $"A message with ID {_startConversationRequest.FirstMessage.Id} already exists."));
        
        var response = await _httpClient.PostAsJsonAsync($"api/Conversations/", _startConversationRequest);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
    
    [Fact]
    public async Task StartConversation_ThirdPartyServiceUnavailable()
    {
        _userConversationServiceMock.Setup(m => m.CreateConversation(It.Is<StartConversationRequest>(
                p => p.Participants.SequenceEqual(_startConversationRequest.Participants) 
                     && p.FirstMessage == _startConversationRequest.FirstMessage)))
            .ThrowsAsync(new ThirdPartyServiceUnavailableException("Third party service is unavailable."));

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
        
        _messageServiceMock.Setup(m => m.GetMessages(_conversationId, _getMessagesParameters))
            .ReturnsAsync(getMessagesServiceResult);

        string nextUri = $"/api/conversations/{_conversationId}/messages" +
                         $"?limit={_getMessagesParameters.Limit}" +
                         $"&continuationToken={WebUtility.UrlEncode(_nextContinuationToken)}" +
                         $"&lastSeenConversationTime={_getUserConversationsParameters.LastSeenConversationTime}";

        var response = await _httpClient.GetAsync($"/api/conversations/{_conversationId}/messages/");
        var json = await response.Content.ReadAsStringAsync();
        var receivedGetMessagesResponse = JsonConvert.DeserializeObject<GetMessagesResponse>(json);
        
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(messages, receivedGetMessagesResponse.Messages);
        Assert.Equal(nextUri, receivedGetMessagesResponse.NextUri);
    }
    
    [Fact]
    public async Task GetMessages_InvalidArguments()
    {
        _messageServiceMock.Setup(m => m.GetMessages(_conversationId, _getMessagesParameters))
            .ThrowsAsync(new ArgumentException());

        var response = await _httpClient.GetAsync($"/api/conversations/{_conversationId}/messages/");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    
    [Fact]
    public async Task GetMessages_ConversationDoesNotExist()
    {
        _messageServiceMock.Setup(m => m.GetMessages(_conversationId, _getMessagesParameters))
            .ThrowsAsync(new ConversationDoesNotExistException(
                $"A conversation partition with the conversationId {_conversationId} does not exist."));

        var response = await _httpClient.GetAsync($"/api/conversations/{_conversationId}/messages/");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
    
    [Fact]
    public async Task GetMessages_ThirdPartyServiceUnavailable()
    {
        _messageServiceMock.Setup(m => m.GetMessages(_conversationId, _getMessagesParameters))
            .ThrowsAsync(new ThirdPartyServiceUnavailableException("Third party service is unavailable."));

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

        _messageServiceMock.Setup(m => m.AddMessage(_conversationId, false, _sendMessageRequest))
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
        _messageServiceMock.Setup(m => m.AddMessage(_conversationId, false, _sendMessageRequest))
            .ThrowsAsync(new ArgumentException());
        
        var response = await _httpClient.PostAsJsonAsync(
            $"/api/conversations/{_conversationId}/messages/", _sendMessageRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    
    [Fact]
    public async Task PostMessage_UserNotParticipant()
    {
        _messageServiceMock.Setup(m => m.AddMessage(_conversationId, false, _sendMessageRequest))
            .ThrowsAsync(new UserNotParticipantException(
                $"User {_username} is not a participant of conversation {_conversationId}."));
        
        var response = await _httpClient.PostAsJsonAsync(
            $"/api/conversations/{_conversationId}/messages/", _sendMessageRequest);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
    
    [Fact]
    public async Task PostMessage_ProfileNotFound()
    {
        _messageServiceMock.Setup(m => m.AddMessage(_conversationId, false, _sendMessageRequest))
            .ThrowsAsync(new UserNotFoundException(
                $"A user with the username {_username} was not found."));
        
        var response = await _httpClient.PostAsJsonAsync(
            $"/api/conversations/{_conversationId}/messages/", _sendMessageRequest);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
    
    [Fact]
    public async Task PostMessage_ConversationDoesNotExist()
    {
        _messageServiceMock.Setup(m => m.AddMessage(_conversationId, false, _sendMessageRequest))
            .ThrowsAsync(new ConversationDoesNotExistException(
                $"A conversation partition with the conversationId {_conversationId} does not exist."));
        
        var response = await _httpClient.PostAsJsonAsync(
            $"/api/conversations/{_conversationId}/messages/", _sendMessageRequest);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
    
    [Fact]
    public async Task PostMessage_MessageExists()
    {
        _messageServiceMock.Setup(m => m.AddMessage(_conversationId, false, _sendMessageRequest))
            .ThrowsAsync(new MessageExistsException($"A message with ID {_sendMessageRequest.Id} already exists."));
        
        var response = await _httpClient.PostAsJsonAsync(
            $"/api/conversations/{_conversationId}/messages/", _sendMessageRequest);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
    
    [Fact]
    public async Task PostMessage_ThirdPartyServiceUnavailable()
    {
        _messageServiceMock.Setup(m => m.AddMessage(_conversationId, false, _sendMessageRequest))
            .ThrowsAsync(new ThirdPartyServiceUnavailableException("Third party service is unavailable."));
        
        var response = await _httpClient.PostAsJsonAsync(
            $"/api/conversations/{_conversationId}/messages/", _sendMessageRequest);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
    
    private static Conversation CreateConversation()
    {
        return new Conversation
        {
            ConversationId = Guid.NewGuid().ToString(),
            LastModifiedUnixTime = _unixTimeNow
        };
    }
}