using ChatService.Web.Dtos;
using ChatService.Web.Storage;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ChatService.Web.IntegrationTests;

public class CosmosProfileStoreTest : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly IProfileStore _store;

    private readonly Profile _profile = new(
        username: Guid.NewGuid().ToString(),
        firstName: "Foo",
        lastName: "Bar",
        profilePictureId: "dummy_id"
    );
    
    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _store.DeleteProfile(_profile.username);
    }

    public CosmosProfileStoreTest(WebApplicationFactory<Program> factory)
    {
        _store = factory.Services.GetRequiredService<IProfileStore>();
    }
    
    [Fact]
    public async Task AddNewProfile_Success()
    {
        await _store.AddProfile(_profile);
        Assert.Equal(_profile, await _store.GetProfile(_profile.username));
    }

    [Theory]
    [InlineData(null, "Foo", "Bar", "dummy_id")]
    [InlineData("", "Foo", "Bar", "dummy_id")]
    [InlineData(" ", "Foo", "Bar", "dummy_id")]
    [InlineData("foobar", null, "Bar", "dummy_id")]
    [InlineData("foobar", "", "Bar", "dummy_id")]
    [InlineData("foobar", " ", "Bar", "dummy_id")]
    [InlineData("foobar", "Foo", null, "dummy_id")]
    [InlineData("foobar", "Foo", "", "dummy_id")]
    [InlineData("foobar", "Foo", " ", "dummy_id")]
    [InlineData("foobar", "Foo", "Bar", null)]
    [InlineData("foobar", "Foo", "Bar","")]
    [InlineData("foobar", "Foo", "Bar"," ")]
    public async Task AddNewProfile_InvalidArgs(string username, string firstName, string lastName, string profilePictureId)
    {
        Profile profile = new(username, firstName, lastName, profilePictureId);
        await Assert.ThrowsAsync<ArgumentException>( async () =>  await _store.AddProfile(profile));
    }
    
    [Fact]
    public async Task AddNewProfile_NullProfile()
    {
        await Assert.ThrowsAsync<ArgumentException>( async () =>  await _store.AddProfile(null));
    }

    [Fact]
    public async Task GetNonExistingProfile()
    {
        Assert.Null(await _store.GetProfile(_profile.username));
    }

    [Fact]
    public async Task DeleteProfile()
    {
        await _store.AddProfile(_profile);
        Assert.Equal(_profile, await _store.GetProfile(_profile.username));
        await _store.DeleteProfile(_profile.username);
        Assert.Null(await _store.GetProfile(_profile.username));
    }
    
    [Fact]
    public async Task ProfileExists_Exists()
    {
        await _store.AddProfile(_profile);
        Assert.True(await _store.ProfileExists(_profile.username));
    }
    
    [Fact]
    public async Task ProfileExists_DoesNotExist()
    {
        Assert.False(await _store.ProfileExists(_profile.username));
    }
}