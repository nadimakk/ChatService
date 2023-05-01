using ChatService.Web.Dtos;
using ChatService.Web.Enums;
using ChatService.Web.Exceptions;
using ChatService.Web.Storage;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ChatService.Web.IntegrationTests;

public class CosmosConversationStoreIntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly IUserConversationStore _userConversationStore;
    
    private static readonly string _username = Guid.NewGuid().ToString();
    
    private GetUserConversationsParameters _parameters = new()
    {
        Limit = 10,
        Order = OrderBy.DESC,
        ContinuationToken = null,
        LastSeenConversationTime = 0
    };
    
    private static readonly UserConversation _userConversation = CreateUserConversation(
        lastModifiedTime: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    private static readonly UserConversation _userConversation1 = CreateUserConversation(lastModifiedTime: 100);
    private static readonly UserConversation _userConversation2 = CreateUserConversation(lastModifiedTime: 200);
    private static readonly UserConversation _userConversation3 = CreateUserConversation(lastModifiedTime: 300);
    
    private List<UserConversation> _userConversations = new() {
        _userConversation1, _userConversation2, _userConversation3, _userConversation
    };
    
    public CosmosConversationStoreIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _userConversationStore = factory.Services.GetRequiredService<IUserConversationStore>();
    }

    [Fact]
    public async Task CreateUserConversation_Success()
    {
        await _userConversationStore.UpsertUserConversation(_userConversation);
        var receivedConversation = await _userConversationStore.GetUserConversation(
            _userConversation.Username, _userConversation.ConversationId);
        
        Assert.Equal(_userConversation, receivedConversation);
    }
    
    [Fact]
    public async Task UpdateUserConversation_Success()
    {
        await _userConversationStore.UpsertUserConversation(_userConversation);
        var receivedConversation = await _userConversationStore.GetUserConversation(
            _userConversation.Username, _userConversation.ConversationId);
        
        Assert.Equal(_userConversation, receivedConversation);

        _userConversation.LastModifiedTime = 100;
        await _userConversationStore.UpsertUserConversation(_userConversation);
        receivedConversation = await _userConversationStore.GetUserConversation(
            _userConversation.Username, _userConversation.ConversationId);
        
        Assert.Equal(_userConversation, receivedConversation);
    }
    
    [Theory]
    [InlineData(null, "dummyConversationId", 100)]
    [InlineData("", "dummyConversationId", 100)]
    [InlineData(" ", "dummyConversationId", 100)]
    [InlineData("foobar", null, 100)]
    [InlineData("foobar", "", 100)]
    [InlineData("foobar", " ", 100)]
    [InlineData("foobar", "dummyConversationId", -100)]
    public async Task CreateUserConversation_InvalidArguments(string username, string conversationId, long lastModifiedTime)
    {
        UserConversation userConversation = new()
        {
            Username = username,
            ConversationId = conversationId,
            LastModifiedTime = lastModifiedTime
        };
        await Assert.ThrowsAsync<ArgumentException>(
            () => _userConversationStore.UpsertUserConversation(userConversation));
    }
    
    [Theory]
    [InlineData(null, "dummyConversationId")]
    [InlineData("", "dummyConversationId")]
    [InlineData(" ", "dummyConversationId")]
    [InlineData("foobar", null)]
    [InlineData("foobar", "")]
    [InlineData("foobar", " ")]
    public async Task GetUserConversation_InvalidArguments(string username, string conversationId)
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _userConversationStore.GetUserConversation(username, conversationId));
    }
    
    [Fact]
    public async Task GetUserConversation_ConversationNotFound()
    { 
        Assert.Null(
            await _userConversationStore.GetUserConversation(_userConversation.Username, _userConversation.ConversationId));
    }
    
    [Fact]
    public async Task GetUserConversations_Limit()
    {
        await AddMultipleUserConversations(_userConversation1, _userConversation2, _userConversation3);

        _parameters.Limit = 1;
        var response = await _userConversationStore.GetUserConversations(_userConversation.Username, _parameters);
        Assert.Single(response.UserConversations);

        _parameters.Limit = 2;
        response = await _userConversationStore.GetUserConversations(_userConversation.Username, _parameters);
        Assert.Equal(2, response.UserConversations.Count);

        _parameters.Limit = 3;
        response = await _userConversationStore.GetUserConversations(_userConversation.Username, _parameters);
        Assert.Equal(3, response.UserConversations.Count);
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

        _parameters.Order = orderBy;
        var response = await _userConversationStore.GetUserConversations(_userConversation.Username, _parameters);
        
        if (orderBy == OrderBy.ASC)
        {
            Assert.Equal(userConversationsExpected, response.UserConversations);
        }
        else
        {
            userConversationsExpected.Reverse();   
            Assert.Equal(userConversationsExpected, response.UserConversations);
        }
    }

    [Fact]
    public async Task GetUserConversations_ContinuationTokenValidity()
    {
        await AddMultipleUserConversations(_userConversation1, _userConversation2, _userConversation3);

        _parameters.Limit = 1;

        var response = await _userConversationStore.GetUserConversations(_userConversation.Username, _parameters);
        
        Assert.Equal(_userConversation3, response.UserConversations.ElementAt(0));

        var nextContinuationToken = response.NextContinuationToken; 
        Assert.NotNull(nextContinuationToken);

        _parameters.ContinuationToken = nextContinuationToken;
        response = await _userConversationStore.GetUserConversations(_userConversation.Username, _parameters);
        Assert.Equal(_userConversation2, response.UserConversations.ElementAt(0));
        
        nextContinuationToken = response.NextContinuationToken; 
        Assert.NotNull(nextContinuationToken);

        _parameters.ContinuationToken = nextContinuationToken;
        response = await _userConversationStore.GetUserConversations(_userConversation.Username, _parameters);
        Assert.Equal(_userConversation1, response.UserConversations.ElementAt(0));
        
        nextContinuationToken = response.NextContinuationToken;
        Assert.Null(nextContinuationToken);
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
        
        foreach (UserConversation userConversation in _userConversations)
        {
            if (userConversation.LastModifiedTime > lastSeenConversationTime)
            {
                userConversationsExpected.Add(userConversation);
            }
        }
        userConversationsExpected.Reverse();
        
        _parameters.LastSeenConversationTime = lastSeenConversationTime;
        var response = await _userConversationStore.GetUserConversations(_userConversation.Username, _parameters);
        
        Assert.Equal(userConversationsExpected, response.UserConversations);
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
        _parameters.Limit = limit;
        _parameters.LastSeenConversationTime = lastSeenConversationTime;
        await Assert.ThrowsAsync<ArgumentException>(
            () => _userConversationStore.GetUserConversations(username, _parameters));
    }

    [Fact]
    public async Task GetUserConversations_InvalidContinuationToken()
    {
        string invalidContinuationToken = Guid.NewGuid().ToString();
        _parameters.ContinuationToken = invalidContinuationToken;
        
        await Assert.ThrowsAsync<InvalidContinuationTokenException>(
            () => _userConversationStore.GetUserConversations(_userConversation.Username, _parameters));
    }

    private async Task AddMultipleUserConversations(params UserConversation[] userConversations)
    {
        foreach (UserConversation userConversation in userConversations)
        {
            await _userConversationStore.UpsertUserConversation(userConversation);
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
    
    private static UserConversation CreateUserConversation(long lastModifiedTime)
    {
        return new UserConversation
        {
            Username = _username,
            ConversationId = Guid.NewGuid().ToString(),
            LastModifiedTime = lastModifiedTime
        };
    }
    
    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await Task.WhenAll(_userConversations.Select(
            userConversation => _userConversationStore.DeleteUserConversation(
                    userConversation.Username, userConversation.ConversationId)));
    }
}