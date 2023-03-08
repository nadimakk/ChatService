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
    private readonly Mock<IImageStore> _imageStoreMock = new();
    private readonly IProfileService _profileService;
    
    private readonly Profile _profile = new Profile("foobar", "Foo", "Bar", "123");

    public ProfileServiceTests(WebApplicationFactory<Program> factory)
    {
        _profileService = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(_profileStoreMock.Object);
                services.AddSingleton(_imageStoreMock.Object);
            });
        }).Services.GetRequiredService<IProfileService>();
    }

    [Fact]
    public async Task GetProfile_Success()
    {
        _profileStoreMock.Setup(m => m.GetProfile(_profile.username))
            .ReturnsAsync(_profile);

        var receivedProfile = await _profileService.GetProfile(_profile.username);
        
        Assert.Equal(_profile, receivedProfile);
    }
    
    [Fact]
    public async Task AddNewProfile_Success()
    {
        _profileStoreMock.Setup(m => m.ProfileExists(_profile.username))
            .ReturnsAsync(false);
        _imageStoreMock.Setup(m => m.ImageExists(_profile.profilePictureId))
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
    [InlineData("foobar", "Foo", "Bar", null)]
    [InlineData("foobar", "Foo", "Bar","")]
    [InlineData("foobar", "Foo", "Bar"," ")]
    public async Task AddNewProfile_InvalidArgs(string username, string firstName, string lastName, string profilePictureId)
    {
        Profile profile = new(username, firstName, lastName, profilePictureId);
        await Assert.ThrowsAsync<ArgumentException>( async () =>  await _profileService.AddProfile(profile));
    }
    
    [Fact]
    public async Task AddNewProfile_UsernameTaken()
    {
        _imageStoreMock.Setup(m => m.ImageExists(_profile.profilePictureId))
            .ReturnsAsync(true);
        _profileStoreMock.Setup(m => m.AddProfile(_profile))
            .ThrowsAsync(new UsernameTakenException($"A profile with username {_profile.username} already exists."));

        await Assert.ThrowsAsync<UsernameTakenException>( async () =>  await _profileService.AddProfile(_profile));
    }
    
    [Fact]
    public async Task AddNewProfile_ProfilePictureNotFound()
    {
        _imageStoreMock.Setup(m => m.ImageExists(_profile.profilePictureId))
            .ReturnsAsync(false);

        await Assert.ThrowsAsync<ImageNotFoundException>( async () =>  await _profileService.AddProfile(_profile));
    }
    
    [Fact]
    public async Task DeleteProfile_Success()
    {
        _profileStoreMock.Setup(m => m.GetProfile(_profile.username))
            .ReturnsAsync(_profile);

        await _profileService.DeleteProfile(_profile.username);
        
        _imageStoreMock.Verify(m => m.DeleteImage(_profile.profilePictureId), Times.Once);
        _profileStoreMock.Verify(m => m.DeleteProfile(_profile.username), Times.Once);
    }
    
    [Fact]
    public async Task DeleteProfile_ProfileNotFound()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _profileService.DeleteProfile(_profile.username));
        
        _imageStoreMock.Verify(m => m.DeleteImage(_profile.profilePictureId), Times.Never);
        _profileStoreMock.Verify(m => m.DeleteProfile(_profile.username), Times.Never);
    }
}