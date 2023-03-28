using ChatService.Web.Dtos;
using ChatService.Web.Enums;
using ChatService.Web.Exceptions;
using ChatService.Web.Storage;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ChatService.Web.IntegrationTests;

public class CosmosConversationStoreTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly IConversationStore _store;
    
    private static readonly UserConversation _userConversation = new UserConversation
    {
        username = Guid.NewGuid().ToString(),
        conversationId = Guid.NewGuid().ToString(),
        lastModifiedTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
    };
    
    private readonly UserConversation _userConversation1 = new UserConversation
    {
        username = _userConversation.username,
        conversationId = Guid.NewGuid().ToString(),
        lastModifiedTime = 100
    };
    
    private readonly UserConversation _userConversation2 = new UserConversation
    {
        username = _userConversation.username,
        conversationId = Guid.NewGuid().ToString(),
        lastModifiedTime = 200
    };
    
    private readonly UserConversation _userConversation3 = new UserConversation
    {
        username = _userConversation.username,
        conversationId = Guid.NewGuid().ToString(),
        lastModifiedTime = 300
    };

    public CosmosConversationStoreTests(WebApplicationFactory<Program> factory)
    {
        _store = factory.Services.GetRequiredService<IConversationStore>();
    }

    [Fact]
    public async Task CreateUserConversation_Successful()
    {
        await _store.CreateUserConversation(_userConversation);
        
        Assert.Equal(_userConversation, await _store.GetUserConversation(_userConversation.username, _userConversation.conversationId));
    }

    [Theory]
    [InlineData(null, "dummy_conversationId")]
    [InlineData("", "dummy_conversationId")]
    [InlineData(" ", "dummy_conversationId")]
    [InlineData("foobar", null)]
    [InlineData("foobar", "")]
    [InlineData("foobar", " ")]
    public async Task CreateUserConversation_InvalidArguments(string username, string conversationId)
    {
        UserConversation userConversation = new()
        {
            username = username,
            conversationId = conversationId
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
            () => _store.GetUserConversation(_userConversation.username, _userConversation.conversationId));
    }
    
    [Fact]
    public async Task GetUserConversations_Limit()
    {
        await AddMultipleUserConversations(_userConversation1, _userConversation2, _userConversation3);
        
        var response = await _store.GetUserConversations(_userConversation.username, 1, OrderBy.ASC, null, 1);
        Assert.Equal(1, response.UserConversations.Count);

        response = await _store.GetUserConversations(_userConversation.username, 2, OrderBy.ASC, null, 1);
        Assert.Equal(2, response.UserConversations.Count);

        response = await _store.GetUserConversations(_userConversation.username, 3, OrderBy.ASC, null, 1);
        Assert.Equal(3, response.UserConversations.Count);
        
        await DeleteMultipleUserConversations(_userConversation1, _userConversation2, _userConversation3);
    }

    [Theory]
    [InlineData(OrderBy.ASC)]
    [InlineData(OrderBy.DESC)]
    public async Task GetUserConversations_OrderBy(OrderBy orderBy)
    {
        UserConversation userConversationSecond = new UserConversation
        {
            username = _userConversation.username,
            conversationId = Guid.NewGuid().ToString(),
            lastModifiedTime = _userConversation.lastModifiedTime
        };

        await _store.CreateUserConversation(_userConversation);
        await _store.CreateUserConversation(userConversationSecond);
        
        List<UserConversation> userConversationsExpected = new();
        userConversationsExpected.Add(_userConversation);
        userConversationsExpected.Add(userConversationSecond);

        var response = await _store.GetUserConversations(_userConversation.username, 2, orderBy, null, 1);
        
        if (orderBy == OrderBy.ASC)
        {
            Assert.Equal(userConversationsExpected, response.UserConversations);
        }
        else
        {
            userConversationsExpected.Reverse();   
            Assert.Equal(userConversationsExpected, response.UserConversations);
        }
        
        await _store.DeleteUserConversation(userConversationSecond.username, userConversationSecond.conversationId);
    }

    [Fact]
    public async Task GetUserConversations_ContinuationTokenValidity()
    {
        await _store.CreateUserConversation(_userConversation1);
        await _store.CreateUserConversation(_userConversation2);
        await _store.CreateUserConversation(_userConversation3);
        
        var response = await _store.GetUserConversations(_userConversation.username, 1, OrderBy.ASC, null, 1);
        
        Assert.Equal(_userConversation1, response.UserConversations.ElementAt(0));

        var nextContinuation = response.NextContinuationToken; 
        Assert.NotNull(nextContinuation);
        
        response = await _store.GetUserConversations(_userConversation.username, 1, OrderBy.ASC, nextContinuation, 1);
        Assert.Equal(_userConversation2, response.UserConversations.ElementAt(0));
        
        nextContinuation = response.NextContinuationToken; 
        Assert.NotNull(nextContinuation);
        
        response = await _store.GetUserConversations(_userConversation.username, 1, OrderBy.ASC, nextContinuation, 1);
        Assert.Equal(_userConversation3, response.UserConversations.ElementAt(0));
        
        nextContinuation = response.NextContinuationToken;
        Assert.Null(nextContinuation);
        
        await _store.DeleteUserConversation(_userConversation.username, _userConversation1.conversationId);
        await _store.DeleteUserConversation(_userConversation.username, _userConversation2.conversationId);
        await _store.DeleteUserConversation(_userConversation.username, _userConversation3.conversationId);
    }

    [Fact]
    public async Task GetUserConversations_LastSeenConversationTime()
    {
        await AddMultipleUserConversations(_userConversation1, _userConversation2, _userConversation3, _userConversation);

        List<UserConversation> UnixTime50Expected = new List<UserConversation> { _userConversation1, _userConversation2, _userConversation3, _userConversation };
        List<UserConversation> UnixTime150Expected = new List<UserConversation> { _userConversation2, _userConversation3, _userConversation };
        List<UserConversation> UnixTime250Expected = new List<UserConversation> { _userConversation3, _userConversation };
        List<UserConversation> UnixTime350Expected = new List<UserConversation> { _userConversation };

        var response = await _store.GetUserConversations(_userConversation.username, 10, OrderBy.ASC, null, 50);
        Assert.Equal(UnixTime50Expected, response.UserConversations);

        response = await _store.GetUserConversations(_userConversation.username, 10, OrderBy.ASC, null, 150);
        Assert.Equal(UnixTime150Expected, response.UserConversations);
        
        response = await _store.GetUserConversations(_userConversation.username, 10, OrderBy.ASC, null, 250);
        Assert.Equal(UnixTime250Expected, response.UserConversations);
        
        response = await _store.GetUserConversations(_userConversation.username, 10, OrderBy.ASC, null, 350);
        Assert.Equal(UnixTime350Expected, response.UserConversations);

        await DeleteMultipleUserConversations(_userConversation1, _userConversation2, _userConversation3);
    }
  
    [Theory]
    [InlineData("", 1)]
    [InlineData(" ", 1)]
    [InlineData(null, 0)]
    [InlineData("username", 0)]
    [InlineData("username", -1)]
    public async Task GetUserConversations_InvalidArguments(string username, int limit)
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _store.GetUserConversations(username, limit, OrderBy.ASC, null, 1));
    }

    private async Task AddMultipleUserConversations(params UserConversation[] userConversations)
    {
        foreach (UserConversation userConversation in userConversations)
        {
            await _store.CreateUserConversation(userConversation);
        }
    }
    
    private async Task DeleteMultipleUserConversations(params UserConversation[] userConversations)
    {
        foreach (UserConversation userConversation in userConversations)
        {
            await _store.DeleteUserConversation(userConversation.username, userConversation.conversationId);
        }
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _store.DeleteUserConversation(_userConversation.username, _userConversation.conversationId);
    }
}