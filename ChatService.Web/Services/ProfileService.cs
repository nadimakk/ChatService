using ChatService.Web.Dtos;
using ChatService.Web.Exceptions;
using ChatService.Web.Storage;
using ChatService.Web.Utilities;

namespace ChatService.Web.Services;

public class ProfileService : IProfileService
{
    private readonly IProfileStore _profileStore;
    private readonly IImageService _imageService;
    
    public ProfileService(IProfileStore profileStore, IImageService imageService)
    {
        _profileStore = profileStore;
        _imageService = imageService;
    }

    public async Task<Profile?> GetProfile(string username)
    {
        ValidateUsername(username);
        
        var profile = await _profileStore.GetProfile(username);
        
        if (profile == null)
        {
            throw new UserNotFoundException(
                $"A profile with the username {username} was not found.");
        }

        return profile;
    }
    
    public async Task AddProfile(Profile profile)
    {
        ValidateProfile(profile);
        // await ThrowIfImageNotFound(profile.ProfilePictureId);
        await _profileStore.AddProfile(profile);
    }

    public async Task<bool> ProfileExists(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException($"Invalid username {username}.");

        }
        return await _profileStore.ProfileExists(username);
    }

    public async Task DeleteProfile(string username)
    {
        Profile? profile = await GetProfile(username);
        if (profile == null)
        {
            throw new UserNotFoundException($"A user with the username {username} was not found.");
        }
        await Task.WhenAll(
            _imageService.DeleteImage(profile.ProfilePictureId),
            _profileStore.DeleteProfile(username)
        );
    }

    private void ValidateUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException($"Invalid username {username}");
        }
    }
    
    private void ValidateProfile(Profile profile)
    {
        if (profile == null ||
            string.IsNullOrWhiteSpace(profile.Username) ||
            string.IsNullOrWhiteSpace(profile.FirstName) ||
            string.IsNullOrWhiteSpace(profile.LastName) 
            // string.IsNullOrWhiteSpace(profile.ProfilePictureId)
           )
        {
            throw new ArgumentException($"Invalid profile {profile}", nameof(profile));
        }
        ConversationIdUtilities.ValidateUsername(profile.Username);
    }
    
    private async Task ThrowIfImageNotFound(string profilePictureId)
    {
        bool imageExists = await _imageService.ImageExists(profilePictureId);
        if (!imageExists)
        {
            throw new ImageNotFoundException($"Profile picture with ID {profilePictureId} was not found.");
        }
    }
    
}