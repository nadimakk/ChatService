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

public class MessageServiceTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly Mock<IMessageStore> _messageStoreMock = new();
    private readonly Mock<IUserConversationStore> _userConversationStoreMock = new();
    private readonly Mock<IProfileService> _profileServiceMock = new();
    private readonly IMessageService _messageService;

    private GetMessagesParameters _parameters = new()
    {
        Limit = 1,
        Order = OrderBy.ASC,
        ContinuationToken = null,
        LastSeenMessageTime = 0
    };
    
    private static readonly string _senderUsername = Guid.NewGuid().ToString();
    private static readonly string _recipientUsername = Guid.NewGuid().ToString();
    private static readonly string _conversationId = 
        ConversationIdUtilities.GenerateConversationId(_senderUsername, _recipientUsername);
    
    private readonly long _unixTimeNow = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    private readonly SendMessageRequest _sendMessageRequest = new()
    {
        Id = Guid.NewGuid().ToString(),
        SenderUsername = _senderUsername,
        Text = Guid.NewGuid().ToString()
    };
    
    public MessageServiceTests(WebApplicationFactory<Program> factory)
    {
        _messageService = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(_messageStoreMock.Object);
                services.AddSingleton(_userConversationStoreMock.Object);
                services.AddSingleton(_profileServiceMock.Object);
            });
        }).Services.GetRequiredService<IMessageService>();
    }
    
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AddMessage_Success(bool isFirstMessage)
    {
        _profileServiceMock.Setup(m => m.ProfileExists(_senderUsername))
            .ReturnsAsync(true);

        _messageStoreMock.Setup(m => m.ConversationPartitionExists(_conversationId))
            .ReturnsAsync(true);

        Message message = new()
        {
            Id = _sendMessageRequest.Id,
            UnixTime = _unixTimeNow,
            SenderUsername = _sendMessageRequest.SenderUsername,
            Text = _sendMessageRequest.Text
        };

        SendMessageResponse expectedSendMessageResponse = new()
        {
            CreatedUnixTime = _unixTimeNow
        };

        SendMessageResponse receivedSendMessageResponse = await _messageService.AddMessage(
            _conversationId, isFirstMessage, _sendMessageRequest);

        _messageStoreMock.Verify(m => m.AddMessage(_conversationId, It.Is<Message>(
            m => m.Id == message.Id
                && m.SenderUsername == message.SenderUsername
                && m.Text == message.Text)), Times.Once);
        
        List<UserConversation> userConversations = CreateUserConversations(_conversationId, _unixTimeNow);
        String username1 = userConversations.ElementAt(0).Username;
        String username2 = userConversations.ElementAt(1).Username;

        _userConversationStoreMock.Verify(m => 
                m.UpsertUserConversation(It.Is<UserConversation>(userConversation => 
                userConversation.Username == username1 && userConversation.ConversationId == _conversationId)), 
            Times.Once);        
        
        _userConversationStoreMock.Verify(m => 
            m.UpsertUserConversation(It.Is<UserConversation>(userConversation => 
                userConversation.Username == username2 && userConversation.ConversationId == _conversationId)), 
            Times.Once);
        
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

        await Assert.ThrowsAsync<ArgumentException>(() => _messageService.AddMessage(
            conversationId, true, sendMessageRequest));
    }

    [Fact]
    public async Task AddMessage_UserNotParticipant()
    {
        _sendMessageRequest.SenderUsername = Guid.NewGuid().ToString();
        
        _messageStoreMock.Setup(m => m.ConversationPartitionExists(_conversationId))
            .ReturnsAsync(true);
        
        _profileServiceMock.Setup(m => m.ProfileExists(_sendMessageRequest.SenderUsername))
            .ReturnsAsync(true);
        
        await Assert.ThrowsAsync<UserNotParticipantException>(
            () => _messageService.AddMessage(_conversationId, true, _sendMessageRequest));
    }

    // [Fact]
    // public async Task AddMessage_ProfileNotFound()
    // {
    //     _profileServiceMock.Setup(m => m.ProfileExists(_senderUsername))
    //         .ReturnsAsync(false);
    //     
    //     await Assert.ThrowsAsync<UserNotFoundException>(() => _messageService.AddMessage(
    //         _conversationId, true, _sendMessageRequest));
    // }
    
    [Fact]
    public async Task AddMessage_ConversationDoesNotExist()
    {
        _profileServiceMock.Setup(m => m.ProfileExists(_senderUsername))
            .ReturnsAsync(true);
    
        _messageStoreMock.Setup(m => m.ConversationPartitionExists(_conversationId))
            .ReturnsAsync(false);
        
        await Assert.ThrowsAsync<ConversationDoesNotExistException>(() => _messageService.AddMessage(
            _conversationId, false, _sendMessageRequest));
    }

    [Fact]
    public async Task GetMessages_Success()
    {
        _messageStoreMock.Setup(m => m.ConversationPartitionExists(_conversationId))
            .ReturnsAsync(true);

        List<Message> messages = new() { CreateMessage(), CreateMessage() };

        string nextContinuationToken = Guid.NewGuid().ToString();
        
        GetMessagesResult getMessagesResult = new()
        {
            Messages = messages,
            NextContinuationToken = nextContinuationToken
        };
        
        _messageStoreMock.Setup(m => m.GetMessages(_conversationId, _parameters))
            .ReturnsAsync(getMessagesResult);

        GetMessagesResult expectedGetMessagesResult = new()
        {
            Messages = messages,
            NextContinuationToken = nextContinuationToken
        };

        _parameters.Limit = 10;
        _parameters.Order = OrderBy.DESC;
        GetMessagesResult receivedGetMessagesResult = await _messageService.GetMessages(
            _conversationId, _parameters);
        
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
        _parameters.Limit = limit;
        _parameters.LastSeenMessageTime = lastSeenConversationTime;
        
        await Assert.ThrowsAsync<ArgumentException>(() => _messageService.GetMessages(conversationId, _parameters));
    }
    
    [Fact]
    public async Task GetMessages_ConversationDoesNotExist()
    {
        _messageStoreMock.Setup(m => m.ConversationPartitionExists(_conversationId))
            .ReturnsAsync(false);
        
        await Assert.ThrowsAsync<ConversationDoesNotExistException>(() => _messageService.GetMessages(
            _conversationId, _parameters));
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
    
    private List<UserConversation> CreateUserConversations(string conversationId, long unixTime)
    {
        string[] usernames = ConversationIdUtilities.SplitConversationId(conversationId);
        List<UserConversation> userConversations = new();
        
        foreach (string username in usernames)
        {
            userConversations.Add(new UserConversation
            {
                ConversationId = conversationId,
                Username = username,
                LastModifiedTime = unixTime
            });
        }

        return userConversations;
    }
}