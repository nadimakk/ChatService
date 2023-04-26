using System.Net;
using System.Net.Http.Json;
using ChatService.Web.Dtos;
using ChatService.Web.Exceptions;
using ChatService.Web.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json;

namespace ChatService.Web.Tests.Controllers;

public class ProfilesControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly Mock<IProfileService> _profileServiceMock = new();
    private readonly HttpClient _httpClient;
    private readonly Profile _profile = new()
    {
        Username = "foobar",
        FirstName = "Foo",
        LastName = "Bar",
        ProfilePictureId = "123"
    };
    
    public ProfilesControllerTests(WebApplicationFactory<Program> factory)
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
        _profileServiceMock.Setup(m => m.GetProfile(_profile.Username))
            .ReturnsAsync(_profile);

        var response = await _httpClient.GetAsync($"api/Profile/{_profile.Username}");
        
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var json = await response.Content.ReadAsStringAsync();
        var receivedProfile = JsonConvert.DeserializeObject<Profile>(json);
        Assert.Equal(_profile, receivedProfile);
    }
    
    [Fact]
    public async Task GetProfile_ProfileNotFound()
    {
        _profileServiceMock.Setup(m => m.GetProfile(_profile.Username))
            .ThrowsAsync(new UserNotFoundException($"A user with the username {_profile.Username} was not found."));

        var response = await _httpClient.GetAsync($"api/Profile/{_profile.Username}");
        
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
    
    [Fact]
    public async Task GetProfile_CosmosServiceUnavailable()
    {
        _profileServiceMock.Setup(m => m.GetProfile(_profile.Username))
            .ThrowsAsync(new CosmosServiceUnavailableException("Cosmos service is unavailable."));

        var response = await _httpClient.GetAsync($"api/Profile/{_profile.Username}");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task PostProfile_Success()
    {
        var response = await _httpClient.PostAsJsonAsync("api/Profile/", _profile);
        
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        
        Assert.Equal($"http://localhost/api/Profile/{_profile.Username}",
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
            .ThrowsAsync(new UsernameTakenException($"The username {_profile.Username} is taken."));
        
        var response = await _httpClient.PostAsJsonAsync("api/Profile/", _profile);
        
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
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
    // [InlineData("foobar", "Foo", "Bar", null)]
    // [InlineData("foobar", "Foo", "Bar", "")]
    // [InlineData("foobar", "Foo", "Bar", " ")]
    public async Task PostProfile_InvalidArguments(string username, string firstName, string lastName, string profilePictureId)
    {
        _profileServiceMock.Setup(m => m.AddProfile(_profile))
            .ThrowsAsync(new ArgumentException($"Invalid profile {_profile}"));
        
        Profile profile = new()
        {
            Username = username,
            FirstName = firstName,
            LastName = lastName,
            ProfilePictureId = profilePictureId
        };

        var response = await _httpClient.PostAsJsonAsync("api/Profile/", profile);
        
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    
    [Fact]
    public async Task PostProfile_InvalidUsername()
    {
        Profile profile = new()
        {
            Username = "username_with_underscore",
            FirstName = "firstName",
            LastName = "lastName",
            ProfilePictureId = "profilePictureId"
        };
        
        _profileServiceMock.Setup(m => m.AddProfile(profile))
            .ThrowsAsync(new InvalidUsernameException($"Username {profile.Username} is invalid. Usernames cannot have an underscore."));

        var response = await _httpClient.PostAsJsonAsync("api/Profile/", profile);
        
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    
    [Fact]
    public async Task ProfileProfile_ProfilePictureNotFound()
    {
        _profileServiceMock.Setup(m => m.AddProfile(_profile))
            .ThrowsAsync(new ImageNotFoundException("Invalid profile picture ID."));
        
        var response = await _httpClient.PostAsJsonAsync("api/Profile/", _profile);
        
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    
    [Fact]
    public async Task ProfileProfile_CosmosServiceUnavailable()
    {
        _profileServiceMock.Setup(m => m.AddProfile(_profile))
            .ThrowsAsync(new CosmosServiceUnavailableException("Cosmos service is unavailable."));

        var response = await _httpClient.PostAsJsonAsync("api/Profile/", _profile);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}