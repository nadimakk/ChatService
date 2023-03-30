using ChatService.Web.Dtos;
using ChatService.Web.Enums;
using ChatService.Web.Exceptions;
using ChatService.Web.Storage;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ChatService.Web.IntegrationTests;

public class CosmosConversationStoreTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly IUserConversationStore _store;
    
    private static readonly UserConversation _userConversation = new UserConversation
    {
        Username = Guid.NewGuid().ToString(),
        ConversationId = Guid.NewGuid().ToString(),
        LastModifiedTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
    };
    
    private readonly UserConversation _userConversation1 = new UserConversation
    {
        Username = _userConversation.Username,
        ConversationId = Guid.NewGuid().ToString(),
        LastModifiedTime = 100
    };
    
    private readonly UserConversation _userConversation2 = new UserConversation
    {
        Username = _userConversation.Username,
        ConversationId = Guid.NewGuid().ToString(),
        LastModifiedTime = 200
    };
    
    private readonly UserConversation _userConversation3 = new UserConversation
    {
        Username = _userConversation.Username,
        ConversationId = Guid.NewGuid().ToString(),
        LastModifiedTime = 300
    };

    public CosmosConversationStoreTests(WebApplicationFactory<Program> factory)
    {
        _store = factory.Services.GetRequiredService<IUserConversationStore>();
    }

    [Fact]
    public async Task CreateUserConversation_Successful()
    {
        await _store.CreateUserConversation(_userConversation);
        
        Assert.Equal(_userConversation, await _store.GetUserConversation(_userConversation.Username, _userConversation.ConversationId));
    }
    
    [Theory]
    [InlineData(null, "dummy_conversationId", 100)]
    [InlineData("", "dummy_conversationId", 100)]
    [InlineData(" ", "dummy_conversationId", 100)]
    [InlineData("foobar", null, 100)]
    [InlineData("foobar", "", 100)]
    [InlineData("foobar", " ", 100)]
    [InlineData("foobar", "dummy_conversationId", -100)]
    public async Task CreateUserConversation_InvalidArguments(string username, string conversationId, long lastModifiedTime)
    {
        UserConversation userConversation = new()
        {
            Username = username,
            ConversationId = conversationId,
            LastModifiedTime = lastModifiedTime
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => _store.CreateUserConversation(userConversation));
    }

    [Fact]
    public async Task CreateUserConversation_ConversationAlreadyExists()
    {
        await _store.CreateUserConversation(_userConversation);

        await Assert.ThrowsAsync<UserConversationExistsException>(
            () => _store.CreateUserConversation(_userConversation));
    }

    [Theory]
    [InlineData(null, "dummy_conversationId")]
    [InlineData("", "dummy_conversationId")]
    [InlineData(" ", "dummy_conversationId")]
    [InlineData("foobar", null)]
    [InlineData("foobar", "")]
    [InlineData("foobar", " ")]
    public async Task GetUserConversation_InvalidArguments(string username, string conversationId)
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _store.GetUserConversation(username, conversationId));
    }
    
    [Fact]
    public async Task GetUserConversation_ConversationNotFound()
    {
        await Assert.ThrowsAsync<UserConversationNotFoundException>(
            () => _store.GetUserConversation(_userConversation.Username, _userConversation.ConversationId));
    }
    
    [Fact]
    public async Task GetUserConversations_Limit()
    {
        await AddMultipleUserConversations(_userConversation1, _userConversation2, _userConversation3);
        
        var response = await _store.GetUserConversations(_userConversation.Username, 1, OrderBy.ASC, null, 1);
        Assert.Equal(1, response.UserConversations.Count);

        response = await _store.GetUserConversations(_userConversation.Username, 2, OrderBy.ASC, null, 1);
        Assert.Equal(2, response.UserConversations.Count);

        response = await _store.GetUserConversations(_userConversation.Username, 3, OrderBy.ASC, null, 1);
        Assert.Equal(3, response.UserConversations.Count);
        
        await DeleteMultipleUserConversations(_userConversation1, _userConversation2, _userConversation3);
    }

    [Theory]
    [InlineData(OrderBy.ASC)]
    [InlineData(OrderBy.DESC)]
    public async Task GetUserConversations_OrderBy(OrderBy orderBy)
    {
        List<UserConversation> userConversationsExpected = CreateListOfUserConversations(
            _userConversation1, _userConversation2, _userConversation3, _userConversation);

        await AddMultipleUserConversations(
            _userConversation, _userConversation1, _userConversation2, _userConversation3);
        
        var response = await _store.GetUserConversations(_userConversation.Username, 10, orderBy, null, 0);
        
        if (orderBy == OrderBy.ASC)
        {
            Assert.Equal(userConversationsExpected, response.UserConversations);
        }
        else
        {
            userConversationsExpected.Reverse();   
            Assert.Equal(userConversationsExpected, response.UserConversations);
        }
        
        await DeleteMultipleUserConversations(_userConversation1, _userConversation2, _userConversation3);
    }

    [Fact]
    public async Task GetUserConversations_ContinuationTokenValidity()
    {
        await AddMultipleUserConversations(_userConversation1, _userConversation2, _userConversation3);
        
        var response = await _store.GetUserConversations(
            _userConversation.Username, 1, OrderBy.ASC, null, 1);
        
        Assert.Equal(_userConversation1, response.UserConversations.ElementAt(0));

        var nextContinuation = response.NextContinuationToken; 
        Assert.NotNull(nextContinuation);
        
        response = await _store.GetUserConversations(_userConversation.Username, 1, OrderBy.ASC, nextContinuation, 1);
        Assert.Equal(_userConversation2, response.UserConversations.ElementAt(0));
        
        nextContinuation = response.NextContinuationToken; 
        Assert.NotNull(nextContinuation);
        
        response = await _store.GetUserConversations(_userConversation.Username, 1, OrderBy.ASC, nextContinuation, 1);
        Assert.Equal(_userConversation3, response.UserConversations.ElementAt(0));
        
        nextContinuation = response.NextContinuationToken;
        Assert.Null(nextContinuation);
        
        await DeleteMultipleUserConversations(_userConversation1, _userConversation2, _userConversation3);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    [InlineData(200)]
    [InlineData(300)]
    public async Task GetUserConversations_LastSeenConversationTime(long lastSeenConversationTime)
    {
        await AddMultipleUserConversations(_userConversation1, _userConversation2, _userConversation3, _userConversation);

        List<UserConversation> userConversationsExpected = new();

        if(_userConversation1.LastModifiedTime > lastSeenConversationTime) { userConversationsExpected.Add(_userConversation1); }
        if(_userConversation2.LastModifiedTime > lastSeenConversationTime) { userConversationsExpected.Add(_userConversation2);}
        if(_userConversation3.LastModifiedTime > lastSeenConversationTime) { userConversationsExpected.Add(_userConversation3);}
        if(_userConversation.LastModifiedTime > lastSeenConversationTime) { userConversationsExpected.Add(_userConversation);}
        
        var response = await _store.GetUserConversations(_userConversation.Username, 10, OrderBy.ASC, null, lastSeenConversationTime);
        
        Assert.Equal(userConversationsExpected, response.UserConversations);

        await DeleteMultipleUserConversations(_userConversation1, _userConversation2, _userConversation3);
    }
  
    [Theory]
    [InlineData("", 1, 100)]
    [InlineData(" ", 1, 100)]
    [InlineData(null, 0, 100)]
    [InlineData("username", 0, 100)]
    [InlineData("username", -1, 100)] 
    [InlineData("username", 10, -100)]
    public async Task GetUserConversations_InvalidArguments(string username, int limit, long lastSeenConversationTime)
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _store.GetUserConversations(username, limit, OrderBy.ASC, null, lastSeenConversationTime));
    }

    private async Task AddMultipleUserConversations(params UserConversation[] userConversations)
    {
        foreach (UserConversation userConversation in userConversations)
        {
            await _store.CreateUserConversation(userConversation);
        }
    }
    
    private List<UserConversation> CreateListOfUserConversations(params UserConversation[] userConversations)
    {
        List<UserConversation> list = new();

        foreach (UserConversation userConversation in userConversations)
        {
            list.Add(userConversation);
        }

        return list;
    }
    
    private async Task DeleteMultipleUserConversations(params UserConversation[] userConversations)
    {
        foreach (UserConversation userConversation in userConversations)
        {
            await _store.DeleteUserConversation(userConversation.Username, userConversation.ConversationId);
        }
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _store.DeleteUserConversation(_userConversation.Username, _userConversation.ConversationId);
    }
}