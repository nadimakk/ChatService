using System.Net;
using System.Net.Http.Json;
using ChatService.Web.Dtos;
using ChatService.Web.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json;

namespace ChatService.Web.Tests.Controllers;

public class ProfileControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private  readonly Mock<IProfileService> _profileServiceMock = new();
    private readonly HttpClient _httpClient;
    private readonly Profile _profile = new Profile("foobar", "Foo", "Bar", "123");
    
    public ProfileControllerTests(WebApplicationFactory<Program> factory)
    {
        _httpClient = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(_profileServiceMock.Object);
            });
        }).CreateClient();
    }

    [Fact]
    public async Task GetProfile_Success()
    {
        _profileServiceMock.Setup(m => m.GetProfile(_profile.username))
            .ReturnsAsync(_profile);

        var response = await _httpClient.GetAsync($"/Profile/{_profile.username}");
        
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var json = await response.Content.ReadAsStringAsync();
        var receivedProfile = JsonConvert.DeserializeObject<Profile>(json);
        Assert.Equal(_profile, receivedProfile);
    }
    
    [Fact]
    public async Task GetProfile_ProfileNotFound()
    {
        _profileServiceMock.Setup(m => m.GetProfile(_profile.username))
            .ReturnsAsync((Profile?) null);

        var response = await _httpClient.GetAsync($"/Profile/{_profile.username}");
        
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        
        var json = await response.Content.ReadAsStringAsync();
        Assert.Equal($"A profile with the username {_profile.username} was not found.", json);
    }

    [Fact]
    public async Task PostProfile_Success()
    {
        var response = await _httpClient.PostAsJsonAsync("/Profile/", _profile);
        
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        
        Assert.Equal($"http://localhost/Profile/{_profile.username}",
            response.Headers.GetValues("Location").First());
        
        var json = await response.Content.ReadAsStringAsync();
        var receivedProfile = JsonConvert.DeserializeObject<Profile>(json);
        Assert.Equal(_profile, receivedProfile);
        
        _profileServiceMock.Verify(mock => mock.AddProfile(_profile), Times.Once);
    }
    
    [Fact]
    public async Task PostProfile_UsernameTaken()
    {
        _profileServiceMock.Setup(m => m.AddProfile(_profile))
            .ThrowsAsync(new ArgumentException($"The username {_profile.username} is taken."));

        var response = await _httpClient.PostAsJsonAsync("/Profile/", _profile);
        
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        
        var json = await response.Content.ReadAsStringAsync();
        Assert.Equal($"The username {_profile.username} is taken.", json);
    }

    [Theory]
    [InlineData(null, "Foo", "Bar", "123")]
    [InlineData("", "Foo", "Bar", "123")]
    [InlineData(" ", "Foo", "Bar", "123")]
    [InlineData("foobar", null, "Bar", "123")]
    [InlineData("foobar", "", "Bar", "123")]
    [InlineData("foobar", " ", "Bar", "123")]
    [InlineData("foobar", "Foo", null, "123")]
    [InlineData("foobar", "Foo", "", "123")]
    [InlineData("foobar", "Foo", " ", "123")]
    [InlineData("foobar", "Foo", "Bar", null)]
    [InlineData("foobar", "Foo", "Bar", "")]
    [InlineData("foobar", "Foo", "Bar", " ")]
    public async Task PostProfile_InvalidArguments(string username, string firstName, string lastName, string profilePictureId)
    {
        _profileServiceMock.Setup(m => m.AddProfile(_profile))
            .ThrowsAsync(new ArgumentException($"Invalid profile {_profile}"));
        
        Profile profile = new(username, firstName, lastName, profilePictureId);

        var response = await _httpClient.PostAsJsonAsync("/Profile", profile);
        
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ProfileProfile_ProfilePictureNotFound()
    {
        _profileServiceMock.Setup(m => m.AddProfile(_profile))
            .ThrowsAsync(new ArgumentException("Invalid profile picture ID."));
        
        var response = await _httpClient.PostAsJsonAsync("/Profile/", _profile);
        
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}