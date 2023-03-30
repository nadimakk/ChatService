using ChatService.Web.Dtos;
using ChatService.Web.Enums;
using ChatService.Web.Exceptions;
using ChatService.Web.Storage;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ChatService.Web.IntegrationTests;

public class CosmosMessageStoreTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly IMessageStore _store;

    private readonly string _conversationId = Guid.NewGuid().ToString();
    
    private readonly Message _message1 = new Message
    {
        id = Guid.NewGuid().ToString(),
        unixTime = 100,
        senderUsername = Guid.NewGuid().ToString(),
        text = "text of _message1"
    };
    
    private readonly Message _message2 = new Message
    {
        id = Guid.NewGuid().ToString(),
        unixTime = 200,
        senderUsername = Guid.NewGuid().ToString(),
        text = "text of _message2"
    };
    
    private readonly Message _message3 = new Message
    {
        id = Guid.NewGuid().ToString(),
        unixTime = 300,
        senderUsername = Guid.NewGuid().ToString(),
        text = "text of _message3"
    };
    
    public CosmosMessageStoreTests(WebApplicationFactory<Program> factory)
    {
        _store = factory.Services.GetRequiredService<IMessageStore>();
    }

    [Fact]
    public async Task AddMessage_Successful()
    {
        await _store.AddMessage(_conversationId, _message1);
        
        Assert.Equal(_message1, await _store.GetMessage(_conversationId, _message1.id));
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
        Message message = new Message
        {
            id = id,
            unixTime = unixTime,
            senderUsername = senderUsername,
            text = text
        };

        await Assert.ThrowsAsync<ArgumentException>(() => _store.AddMessage(_conversationId, message));
    }
    
    [Fact]
    public async Task AddMessage_MessageAlreadyExists()
    {
        await _store.AddMessage(_conversationId, _message1);
        
        await Assert.ThrowsAsync<MessageExistsException>(() => _store.AddMessage(_conversationId, _message1));
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
        await Assert.ThrowsAsync<ArgumentException>(() => _store.GetMessage(conversationId, messageId));
    }
    
    [Fact]
    public async Task GetMessage_MessageNotFound()
    {
        await Assert.ThrowsAsync<MessageNotFoundException>(() => _store.GetMessage(_conversationId, _message1.id));
    }

    [Fact]
    public async Task GetMessages_Limit()
    {
        await AddMultipleMessages(_conversationId, _message1, _message2, _message3);

        var response = await _store.GetMessages(
            _conversationId, 1, OrderBy.ASC, null, 1);
        Assert.Equal(1, response.Messages.Count);

        response = await _store.GetMessages(_conversationId, 2, OrderBy.ASC, null, 1);
        Assert.Equal(2, response.Messages.Count);
        
        response = await _store.GetMessages(_conversationId, 3, OrderBy.ASC, null, 1);
        Assert.Equal(3, response.Messages.Count);
        
        await DeleteMultipleMessages(_conversationId, _message1, _message2, _message3);
    }

    [Theory]
    [InlineData(OrderBy.ASC)]
    [InlineData(OrderBy.DESC)]
    public async Task GetMessages_OrderBy(OrderBy orderBy)
    {
        await AddMultipleMessages(_conversationId, _message1, _message2);

        List<Message> messagesExpected = new();
        messagesExpected.Add(_message1);
        messagesExpected.Add(_message2);

        var response = await _store.GetMessages(
            _conversationId, 10, orderBy, null, 1);

        if (orderBy == OrderBy.ASC)
        {
            Assert.Equal(messagesExpected, response.Messages);
        }
        else
        {
            messagesExpected.Reverse();
            Assert.Equal(messagesExpected, response.Messages);
        }
        
        await _store.DeleteMessage(_conversationId, _message2.id);
    }

    [Fact]
    public async Task GetMessages_ContinuationTokenValidity()
    {
        await AddMultipleMessages(_conversationId, _message1, _message2, _message3);

        var response = await _store.GetMessages(
            _conversationId, 1, OrderBy.ASC, null, 1);
        Assert.Equal(_message1, response.Messages.ElementAt(0));
        Assert.NotNull(response.NextContinuationToken);
        
        response = await _store.GetMessages(
            _conversationId, 1, OrderBy.ASC, response.NextContinuationToken, 1);
        Assert.Equal(_message2, response.Messages.ElementAt(0));
        Assert.NotNull(response.NextContinuationToken);
        
        response = await _store.GetMessages(
            _conversationId, 1, OrderBy.ASC, response.NextContinuationToken, 1);
        Assert.Equal(_message3, response.Messages.ElementAt(0));
        Assert.Null(response.NextContinuationToken);
        
        await DeleteMultipleMessages(_conversationId, _message2, _message3);
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
        if(_message1.unixTime > lastSeenMessageTime) messagesExpected.Add(_message1);
        if(_message2.unixTime > lastSeenMessageTime) messagesExpected.Add(_message2);
        if(_message3.unixTime > lastSeenMessageTime) messagesExpected.Add(_message3);

        var response = await _store.GetMessages(
            _conversationId, 10, OrderBy.ASC, null, lastSeenMessageTime);
        
        Assert.Equal(messagesExpected, response.Messages);
        
        await DeleteMultipleMessages(_conversationId, _message2, _message3);
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
        Assert.ThrowsAsync<ArgumentException>(() =>
            _store.GetMessages(conversationId, limit, OrderBy.ASC, null, lastSeenMessageTime));
    }

    [Fact]
    public async Task ConversationPartitionExists_Exists()
    {
        await _store.AddMessage(_conversationId, _message1);
        
        Assert.True(await _store.ConversationPartitionExists(_conversationId));
    }
    
    [Fact]
    public async Task ConversationPartitionExists_DoesNotExists()
    {
        Assert.False(await _store.ConversationPartitionExists(_conversationId));
    }
    
    private async Task AddMultipleMessages(string conversationId, params Message[] messages)
    {
        foreach (Message message in messages)
        {
            await _store.AddMessage(conversationId, message);
        }
    }
    
    private async Task DeleteMultipleMessages(string conversationId, params Message[] messages)
    {
        foreach (Message message in messages)
        {
            await _store.DeleteMessage(conversationId, message.id);
        }
    }
    
    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _store.DeleteMessage(_conversationId, _message1.id);
    }
}