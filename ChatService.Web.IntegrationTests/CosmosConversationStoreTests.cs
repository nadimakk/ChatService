using ChatService.Web.Dtos;
using ChatService.Web.Enums;
using ChatService.Web.Storage;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ChatService.Web.IntegrationTests;

public class CosmosConversationStoreTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly IConversationStore _store;
    
    private readonly UserConversation _userConversation = new UserConversation
    {
        username = Guid.NewGuid().ToString(),
        conversationId = Guid.NewGuid().ToString(),
        lastModifiedTime = 1
    };

    public CosmosConversationStoreTests(WebApplicationFactory<Program> factory)
    {
        _store = factory.Services.GetRequiredService<IConversationStore>();
    }
    
    [Theory]
    [InlineData(OrderBy.ASC)]
    [InlineData(OrderBy.DESC)]
    public async Task GetUserConversations_Ordered_Successful(OrderBy orderBy)
    {
        UserConversation userConversationSecond = new UserConversation
        {
            username = _userConversation.username,
            conversationId = Guid.NewGuid().ToString(),
            lastModifiedTime = 2
        };

        await _store.CreateUserConversation(_userConversation);
        await _store.CreateUserConversation(userConversationSecond);
        
        List<UserConversation> userConversationsExpected = new();
        userConversationsExpected.Add(_userConversation);
        userConversationsExpected.Add(userConversationSecond);

        var response = await _store.GetUserConversations(_userConversation.username, 1, orderBy, null, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        
        if (orderBy == OrderBy.ASC)
        {
            Assert.Equal(response.UserConversations, userConversationsExpected);
        }
        else
        {
            userConversationsExpected.Reverse();   
            Assert.Equal(response.UserConversations, userConversationsExpected);
        }
        
        await _store.DeleteUserConversation(userConversationSecond.username, userConversationSecond.conversationId);
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

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _store.DeleteUserConversation(_userConversation.username, _userConversation.conversationId);
    }
}