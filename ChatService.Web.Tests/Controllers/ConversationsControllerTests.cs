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

    private static readonly SendMessageRequest _sendMessageRequest = new SendMessageRequest
    {
        MessageId = Guid.NewGuid().ToString(),
        SenderUsername = _username,
        Text = "Hello"
    };
    
    private readonly string _conversationId = Guid.NewGuid().ToString();
    
    private readonly long _unixTimeNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    
    private readonly string _nextContinuationToken = Guid.NewGuid().ToString();

    private readonly StartConversationRequest _startConversationRequest = new StartConversationRequest
    {
        Participants = new List<string> { _username, Guid.NewGuid().ToString() },
        FirstMessage = _sendMessageRequest
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
        List<Conversation> conversations = new();
        conversations.Add(new Conversation
        {
            ConversationId = Guid.NewGuid().ToString(),
            LastModifiedUnixTime = _unixTimeNow
        });
        conversations.Add(new Conversation
        {
            ConversationId = Guid.NewGuid().ToString(),
            LastModifiedUnixTime = _unixTimeNow
        });
        
        var userConversationServiceResult = new GetUserConversationsServiceResult
        {
            Conversations = conversations,
            NextContinuationToken = _nextContinuationToken
        };
        
        _userConversationServiceMock.Setup(m => m.GetUserConversations(_username, 10, OrderBy.DESC, null, 0))
            .ReturnsAsync(userConversationServiceResult);
        
        string nextUri = "/api/conversations" +
                         $"?username={_username}" +
                         "&limit=10" +
                         "&lastSeenConversationTime=0" +
                         $"&continuationToken={_nextContinuationToken}";

        var response = await _httpClient.GetAsync($"api/Conversations/?username={_username}");
        var json = await response.Content.ReadAsStringAsync();
        var receivedGetUserConversationsResponse = JsonConvert.DeserializeObject<GetUserConversationsResponse>(json);
        
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(conversations, receivedGetUserConversationsResponse.Conversations);
        Assert.Equal(nextUri, receivedGetUserConversationsResponse.NextUri);
    }
    
    [Fact]
    public async Task GetUserConversations_InvalidArguments()
    {
        _userConversationServiceMock.Setup(m => m.GetUserConversations(_username, 10, OrderBy.DESC, null, 0))
            .ThrowsAsync(new ArgumentException($"Invalid arguments."));

        var response = await _httpClient.GetAsync($"api/Conversations/?username={_username}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    
    [Fact]
    public async Task GetUserConversations_UserNotFound()
    {
        _userConversationServiceMock.Setup(m => m.GetUserConversations(_username, 10, OrderBy.DESC, null, 0))
            .ThrowsAsync(new UserNotFoundException($"User {_username} was not found."));

        var response = await _httpClient.GetAsync($"api/Conversations/?username={_username}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
    
    [Fact]
    public async Task StartConversation_Success()
    {
        var startConversationResponse = new StartConversationResponse
        {
            ConversationId = Guid.NewGuid().ToString(),
            CreatedUnixTime = _unixTimeNow
        };

        _userConversationServiceMock.Setup(m => m.CreateConversation(It.Is<StartConversationRequest>(
                    p => p.Participants.SequenceEqual(_startConversationRequest.Participants) 
                         && p.FirstMessage == _startConversationRequest.FirstMessage)))
            .ReturnsAsync(startConversationResponse);
        
        var response = await _httpClient.PostAsJsonAsync($"api/Conversations/", _startConversationRequest);
        var json = await response.Content.ReadAsStringAsync();
        var receivedStartConversationResponse = JsonConvert.DeserializeObject<StartConversationResponse>(json);
        
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(startConversationResponse, receivedStartConversationResponse);
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
            .ThrowsAsync(new ProfileNotFoundException($"A profile with the username {_username} was not found."));
        
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
            $"A message with ID {_startConversationRequest.FirstMessage.MessageId} already exists."));
        
        var response = await _httpClient.PostAsJsonAsync($"api/Conversations/", _startConversationRequest);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
    
    [Fact]
    public async Task GetMessages_Success()
    {
        List<Message> messages = new();
        messages.Add(new Message
        {
            MessageId = Guid.NewGuid().ToString(),
            SenderUsername = Guid.NewGuid().ToString(),
            UnixTime = _unixTimeNow
        });
        messages.Add(new Message
        {
            MessageId = Guid.NewGuid().ToString(),
            SenderUsername = Guid.NewGuid().ToString(),
            UnixTime = _unixTimeNow
        });

        var getMessagesServiceResult = new GetMessagesServiceResult
        {
            Messages = messages,
            NextContinuationToken = _nextContinuationToken
        };
        
        _messageServiceMock.Setup(m => m.GetMessages(_conversationId, 10, OrderBy.DESC, null, 0))
            .ReturnsAsync(getMessagesServiceResult);
        
        string nextUri = $"/api/conversations/{_conversationId}/messages" +
                         "&limit=10" +
                         $"&continuationToken={_nextContinuationToken}" +
                         "&lastSeenConversationTime=0";

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
        _messageServiceMock.Setup(m => m.GetMessages(_conversationId, 10, OrderBy.DESC, null, 0))
            .ThrowsAsync(new ArgumentException());

        var response = await _httpClient.GetAsync($"/api/conversations/{_conversationId}/messages/");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    
    [Fact]
    public async Task GetMessages_ConversationDoesNotExist()
    {
        _messageServiceMock.Setup(m => m.GetMessages(_conversationId, 10, OrderBy.DESC, null, 0))
            .ThrowsAsync(new ConversationDoesNotExistException(
                $"A conversation partition with the conversationId {_conversationId} does not exist."));

        var response = await _httpClient.GetAsync($"/api/conversations/{_conversationId}/messages/");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
            .ThrowsAsync(new ProfileNotFoundException(
                $"A profile with the username {_username} was not found."));
        
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
            .ThrowsAsync(new MessageExistsException($"A message with ID {_sendMessageRequest.MessageId} already exists."));
        
        var response = await _httpClient.PostAsJsonAsync(
            $"/api/conversations/{_conversationId}/messages/", _sendMessageRequest);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}