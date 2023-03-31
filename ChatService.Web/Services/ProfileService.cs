using ChatService.Web.Dtos;
using ChatService.Web.Exceptions;
using ChatService.Web.Storage;

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
            throw new ProfileNotFoundException(
                $"A profile with the username {username} was not found.");
        }

        return profile;
    }
    
    public async Task AddProfile(Profile profile)
    {
        ValidateProfile(profile);

        bool imageExists = await _imageService.ImageExists(profile.ProfilePictureId);
        if (!imageExists)
        {
            throw new ImageNotFoundException($"Profile picture with ID {profile.ProfilePictureId} was not found.");
        }
        
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
            throw new ProfileNotFoundException($"Profile with username {username} does not exist.");
        }
        await _imageService.DeleteImage(profile.ProfilePictureId);
        await _profileStore.DeleteProfile(username);
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
            string.IsNullOrWhiteSpace(profile.LastName) ||
            string.IsNullOrWhiteSpace(profile.ProfilePictureId)
           )
        {
            throw new ArgumentException($"Invalid profile {profile}", nameof(profile));
        }
        if (profile.Username.Contains('_'))
        {
            throw new InvalidUsernameException($"Username {profile.Username} is invalid. Usernames cannot have an underscore.");
        }
    }
}