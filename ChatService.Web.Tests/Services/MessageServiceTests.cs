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
    private readonly Mock<IProfileService> _profileServiceMock = new();
    private readonly IMessageService _messageService;

    private static readonly string _senderUsername = Guid.NewGuid().ToString();
    
    private static readonly string _recipientUsername = Guid.NewGuid().ToString();
    
    private static readonly string _conversationId = ConversationIdUtilities.GenerateConversationId(_senderUsername, _recipientUsername);

    private readonly long _unixTimeNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private readonly SendMessageRequest _sendMessageRequest = new SendMessageRequest
    {
        MessageId = Guid.NewGuid().ToString(),
        SenderUsername = _senderUsername,
        Text = "Hello"
    };
    
    public MessageServiceTests(WebApplicationFactory<Program> factory)
    {
        _messageService = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(_messageStoreMock.Object);
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

        Message message = new Message
        {
            MessageId = _sendMessageRequest.MessageId,
            UnixTime = _unixTimeNow,
            SenderUsername = _sendMessageRequest.SenderUsername,
            Text = _sendMessageRequest.Text
        };

        SendMessageResponse expectedSendMessageResponse = new SendMessageResponse
        {
            CreatedUnixTime = _unixTimeNow
        };

        SendMessageResponse receivedSendMessageResponse = await _messageService.AddMessage(
            _conversationId, isFirstMessage, _sendMessageRequest);

        _messageStoreMock.Verify(m => m.AddMessage(_conversationId, It.Is<Message>(
            m => m.MessageId == message.MessageId
                && m.SenderUsername == message.SenderUsername
                && m.Text == message.Text)), Times.Once);
        
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
        SendMessageRequest sendMessageRequest = new SendMessageRequest
        {
            MessageId = messageId,
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
        
        await Assert.ThrowsAsync<UserNotParticipantException>(() => _messageService.AddMessage(
            _conversationId, true, _sendMessageRequest));
    }

    [Fact]
    public async Task AddMessage_ProfileNotFound()
    {
        _profileServiceMock.Setup(m => m.ProfileExists(_senderUsername))
            .ReturnsAsync(false);
        
        await Assert.ThrowsAsync<ProfileNotFoundException>(() => _messageService.AddMessage(
            _conversationId, true, _sendMessageRequest));
    }
    
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

        List<Message> messages = new List<Message> { 
            new Message
            {
                MessageId = Guid.NewGuid().ToString(),
                UnixTime = _unixTimeNow,
                SenderUsername = _senderUsername,
                Text = "Hello"
            },
            new Message
            {
                MessageId = Guid.NewGuid().ToString(),
                UnixTime = _unixTimeNow,
                SenderUsername = _senderUsername,
                Text = "Good Bye"
            }
        };

        string nextContinuationToken = Guid.NewGuid().ToString();

        _messageStoreMock.Setup(m => m.GetMessages(_conversationId, 10, OrderBy.DESC, null, 0))
            .ReturnsAsync((messages, nextContinuationToken));

        GetMessagesServiceResult expectedGetMessagesServiceResult = new GetMessagesServiceResult
        {
            Messages = messages,
            NextContinuationToken = nextContinuationToken
        };

        GetMessagesServiceResult receivedGetMessagesServiceResult = await _messageService.GetMessages(
            _conversationId, 10, OrderBy.DESC, null, 0);
        
        Assert.Equal(expectedGetMessagesServiceResult.Messages, receivedGetMessagesServiceResult.Messages);
        Assert.Equal(expectedGetMessagesServiceResult.NextContinuationToken, receivedGetMessagesServiceResult.NextContinuationToken);
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
        await Assert.ThrowsAsync<ArgumentException>(() => _messageService.GetMessages(conversationId, limit,
            OrderBy.DESC, null, lastSeenConversationTime));
    }
    
    [Fact]
    public async Task GetMessages_ConversationDoesNotExist()
    {
        _messageStoreMock.Setup(m => m.ConversationPartitionExists(_conversationId))
            .ReturnsAsync(false);
        
        await Assert.ThrowsAsync<ConversationDoesNotExistException>(() => _messageService.GetMessages(
            _conversationId, 1, OrderBy.DESC, null, 0));
    }
}