using ChatService.Web.Dtos;
using ChatService.Web.Exceptions;
using ChatService.Web.Services;
using ChatService.Web.Storage;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace ChatService.Web.Tests.Services;

public class ProfileServiceTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly Mock<IProfileStore> _profileStoreMock = new();
    private readonly Mock<IImageService> _imageServiceMock = new();
    private readonly IProfileService _profileService;
    
    private readonly Profile _profile = new()
    {
        Username = Guid.NewGuid().ToString(),
        FirstName = Guid.NewGuid().ToString(),
        LastName = Guid.NewGuid().ToString(),
        ProfilePictureId = Guid.NewGuid().ToString()
    };

    public ProfileServiceTests(WebApplicationFactory<Program> factory)
    {
        _profileService = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(_profileStoreMock.Object);
                services.AddSingleton(_imageServiceMock.Object);
            });
        }).Services.GetRequiredService<IProfileService>();
    }

    [Fact]
    public async Task GetProfile_Success()
    {
        _profileStoreMock.Setup(m => m.GetProfile(_profile.Username))
            .ReturnsAsync(_profile);

        var receivedProfile = await _profileService.GetProfile(_profile.Username);
        
        Assert.Equal(_profile, receivedProfile);
    }
    
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task GetProfile_InvalidArguments(string username)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _profileService.GetProfile(username));
    }
    
    [Fact]
    public async Task AddNewProfile_Success()
    {
        _profileStoreMock.Setup(m => m.ProfileExists(_profile.Username))
            .ReturnsAsync(false);
        _imageServiceMock.Setup(m => m.ImageExists(_profile.ProfilePictureId))
            .ReturnsAsync(true);

        await _profileService.AddProfile(_profile);
        
        _profileStoreMock.Verify(m => m.AddProfile(_profile), Times.Once);
    }
    
    [Fact]
    public async Task AddNewProfile_NullProfile()
    {
        await Assert.ThrowsAsync<ArgumentException>( async () =>  await _profileService.AddProfile(null));
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
    // [InlineData("foobar", "Foo", "Bar", null)]
    // [InlineData("foobar", "Foo", "Bar","")]
    // [InlineData("foobar", "Foo", "Bar"," ")]
    public async Task AddNewProfile_InvalidArgs(string username, string firstName, string lastName, string profilePictureId)
    {
        Profile profile = new()
        {
            Username = username,
            FirstName = firstName,
            LastName = lastName,
            ProfilePictureId = profilePictureId
        };
        
        await Assert.ThrowsAsync<ArgumentException>( async () =>  await _profileService.AddProfile(profile));
    }
    
    [Fact]
    public async Task AddNewProfile_UsernameTaken()
    {
        _imageServiceMock.Setup(m => m.ImageExists(_profile.ProfilePictureId))
            .ReturnsAsync(true);
        _profileStoreMock.Setup(m => m.AddProfile(_profile))
            .ThrowsAsync(new UsernameTakenException($"A profile with username {_profile.Username} already exists."));

        await Assert.ThrowsAsync<UsernameTakenException>( async () =>  await _profileService.AddProfile(_profile));
    }
    
    [Fact]
    public async Task AddNewProfile_InvalidUsername()
    {
        Profile profile = new()
        {
            Username = "username_with_underscore",
            FirstName = "firstName",
            LastName = "lastName",
            ProfilePictureId = "profilePictureId"
        };
        
        await Assert.ThrowsAsync<InvalidUsernameException>( async () =>  await _profileService.AddProfile(profile));
    }
    
    // [Fact]
    // public async Task AddNewProfile_ProfilePictureNotFound()
    // {
    //     _imageServiceMock.Setup(m => m.ImageExists(_profile.ProfilePictureId))
    //         .ReturnsAsync(false);
    //
    //     await Assert.ThrowsAsync<ImageNotFoundException>( async () =>  await _profileService.AddProfile(_profile));
    // }
    
    [Fact]
    public async Task DeleteProfile_Success()
    {
        _profileStoreMock.Setup(m => m.GetProfile(_profile.Username))
            .ReturnsAsync(_profile);

        await _profileService.DeleteProfile(_profile.Username);
        
        _imageServiceMock.Verify(m => m.DeleteImage(_profile.ProfilePictureId), Times.Once);
        _profileStoreMock.Verify(m => m.DeleteProfile(_profile.Username), Times.Once);
    }
    
    [Fact]
    public async Task DeleteProfile_ProfileNotFound()
    {
        await Assert.ThrowsAsync<UserNotFoundException>(
            async () => await _profileService.DeleteProfile(_profile.Username));
        
        _imageServiceMock.Verify(m => m.DeleteImage(_profile.ProfilePictureId), Times.Never);
        _profileStoreMock.Verify(m => m.DeleteProfile(_profile.Username), Times.Never);
    }
}