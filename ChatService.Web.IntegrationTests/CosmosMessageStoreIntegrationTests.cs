using ChatService.Web.Dtos;
using ChatService.Web.Enums;
using ChatService.Web.Exceptions;
using ChatService.Web.Storage;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ChatService.Web.IntegrationTests;

public class CosmosMessageStoreIntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly IMessageStore _messageStore;

    private readonly string _conversationId = Guid.NewGuid().ToString();
    
    private GetMessagesParameters _parameters = new()
    {
        Limit = 1,
        Order = OrderBy.ASC,
        ContinuationToken = null,
        LastSeenMessageTime = 0
    };
    
    private static readonly Message _message1 = CreateMessage(unixTime: 100, text: "text of _message1");
    private static readonly Message _message2 = CreateMessage(unixTime: 200, text: "text of _message2");
    private static readonly Message _message3 = CreateMessage(unixTime: 300, text: "text of _message3");

    private List<Message> _messages = new() { _message1, _message2, _message3 };
    
    public CosmosMessageStoreIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _messageStore = factory.Services.GetRequiredService<IMessageStore>();
    }

    [Fact]
    public async Task AddMessage_Success()
    {
        await _messageStore.AddMessage(_conversationId, _message1);
        var receivedMessage = await _messageStore.GetMessage(_conversationId, _message1.Id);
        Assert.Equal(_message1, receivedMessage);
    }

    [Theory]
    [InlineData(null, "senderUsername", "text", 100)]
    [InlineData("", "senderUsername", "text", 100)]
    [InlineData(" ", "senderUsername", "text", 100)]
    [InlineData("id", null, "text", 100)]
    [InlineData("id", "", "text", 100)]
    [InlineData("id", " ", "text", 100)]
    [InlineData("id", "senderUsername", null, 100)]
    [InlineData("id", "senderUsername", "", 100)]
    [InlineData("id", "senderUsername", " ", 100)]
    [InlineData("id", "senderUsername", "text", -100)]
    public async Task AddMessage_InvalidArguments(string id, string senderUsername, string text, long unixTime)
    {
        Message message = new()
        {
            Id = id,
            UnixTime = unixTime,
            SenderUsername = senderUsername,
            Text = text
        };
        
        await Assert.ThrowsAsync<ArgumentException>(() => _messageStore.AddMessage(_conversationId, message));
    }
    
    [Fact]
    public async Task AddMessage_MessageAlreadyExists()
    {
        await _messageStore.AddMessage(_conversationId, _message1);
        await Assert.ThrowsAsync<MessageExistsException>(() => _messageStore.AddMessage(_conversationId, _message1));
    }

    [Fact]
    public async Task UpdateMessageTime_Success()
    {
        await _messageStore.AddMessage(_conversationId, _message1);
        var receivedMessage = await _messageStore.GetMessage(_conversationId, _message1.Id);
        Assert.Equal(_message1, receivedMessage);

        _message1.UnixTime = 200;
        await _messageStore.UpdateMessageTime(_conversationId, _message1);
        receivedMessage = await _messageStore.GetMessage(_conversationId, _message1.Id);
        Assert.Equal(_message1, receivedMessage);
    }

    [Theory]
    [InlineData(null, "messageId")]
    [InlineData("", "messageId")]
    [InlineData(" ", "messageId")]
    [InlineData("conversationId", null)]
    [InlineData("conversationId", "")]
    [InlineData("conversationId", " ")]
    public async Task GetMessage_InvalidArguments(string conversationId, string messageId)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _messageStore.GetMessage(conversationId, messageId));
    }
    
    [Fact]
    public async Task GetMessage_MessageNotFound()
    {
        var message = await _messageStore.GetMessage(_conversationId, _message1.Id);
        Assert.Null(message);
    }

    [Fact]
    public async Task GetMessages_Limit()
    {
        await AddMultipleMessages(_conversationId, _message1, _message2, _message3);
        
        var response = await _messageStore.GetMessages(_conversationId, _parameters);
        Assert.Single(response.Messages);

        _parameters.Limit = 2;
        response = await _messageStore.GetMessages(_conversationId, _parameters);
        Assert.Equal(2, response.Messages.Count);

        _parameters.Limit = 3;
        response = await _messageStore.GetMessages(_conversationId, _parameters);
        Assert.Equal(3, response.Messages.Count);
    }

    [Theory]
    [InlineData(OrderBy.ASC)]
    [InlineData(OrderBy.DESC)]
    public async Task GetMessages_OrderBy(OrderBy orderBy)
    {
        await AddMultipleMessages(_conversationId, _message1, _message2);

        List<Message> messagesExpected = new() { _message1, _message2 };

        _parameters.Limit = 10;
        _parameters.Order = orderBy;
        var response = await _messageStore.GetMessages(_conversationId, _parameters);

        if (orderBy == OrderBy.ASC)
        {
            Assert.Equal(messagesExpected, response.Messages);
        }
        else
        {
            messagesExpected.Reverse();
            Assert.Equal(messagesExpected, response.Messages);
        }
    }

    [Fact]
    public async Task GetMessages_ContinuationTokenValidity()
    {
        await AddMultipleMessages(_conversationId, _message1, _message2, _message3);

        var response = await _messageStore.GetMessages(_conversationId, _parameters);
        Assert.Equal(_message1, response.Messages.ElementAt(0));
        Assert.NotNull(response.NextContinuationToken);

        _parameters.ContinuationToken = response.NextContinuationToken;
        response = await _messageStore.GetMessages(_conversationId, _parameters);
        Assert.Equal(_message2, response.Messages.ElementAt(0));
        Assert.NotNull(response.NextContinuationToken);
        
        _parameters.ContinuationToken = response.NextContinuationToken;
        response = await _messageStore.GetMessages(_conversationId, _parameters);
        Assert.Equal(_message3, response.Messages.ElementAt(0));
        Assert.Null(response.NextContinuationToken);
    }
    
    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    [InlineData(200)]
    [InlineData(300)]
    public async Task GetMessages_LastSeenMessageTime(long lastSeenMessageTime)
    {
        await AddMultipleMessages(_conversationId, _message1, _message2, _message3);

        List<Message> messagesExpected = new();
        foreach (Message message in _messages)
        {
            if (message.UnixTime > lastSeenMessageTime)
            {
                messagesExpected.Add(message);
            }
        }

        _parameters.Limit = 10;
        _parameters.LastSeenMessageTime = lastSeenMessageTime;
        var response = await _messageStore.GetMessages(_conversationId, _parameters);
        
        Assert.Equal(messagesExpected, response.Messages);
    }

    [Theory]
    [InlineData(null, 10, 100)]
    [InlineData("", 10, 100)]
    [InlineData(" ", 10, 100)]
    [InlineData("conversationId", 0, 100)]
    [InlineData("conversationId", -10, 100)]
    [InlineData("conversationId", 10, -100)]
    public async Task GetMessages_InvalidArguments(string conversationId, int limit, long lastSeenMessageTime)
    {
        _parameters.Limit = limit;
        _parameters.LastSeenMessageTime = lastSeenMessageTime;
        await Assert.ThrowsAsync<ArgumentException>(() => _messageStore.GetMessages(conversationId, _parameters));
    }
    
    [Fact]
    public async Task GetMessages_InvalidContinuationToken()
    {
        string invalidContinuationToken = Guid.NewGuid().ToString();
        _parameters.Limit = 10;
        _parameters.Order = OrderBy.DESC;
        _parameters.ContinuationToken = invalidContinuationToken;
        await Assert.ThrowsAsync<InvalidContinuationTokenException>(
            () => _messageStore.GetMessages(_conversationId, _parameters));
    }

    [Fact]
    public async Task ConversationPartitionExists_Exists()
    {
        await _messageStore.AddMessage(_conversationId, _message1);
        Assert.True(await _messageStore.ConversationExists(_conversationId));
    }
    
    [Fact]
    public async Task ConversationPartitionExists_DoesNotExists()
    {
        Assert.False(await _messageStore.ConversationExists(_conversationId));
    }
    
    private async Task AddMultipleMessages(string conversationId, params Message[] messages)
    {
        foreach (Message message in messages)
        {
            await _messageStore.AddMessage(conversationId, message);
        }
    }
    
    private static Message CreateMessage(int unixTime, string text)
    {
        return new Message()
        {
            Id = Guid.NewGuid().ToString(),
            UnixTime = unixTime,
            SenderUsername = Guid.NewGuid().ToString(),
            Text = text
        };
    }
    
    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await Task.WhenAll(_messages.Select(
            message => _messageStore.DeleteMessage(_conversationId, message.Id)));
    }
}