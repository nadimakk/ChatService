using ChatService.Web.Dtos;
using ChatService.Web.Exceptions;
using ChatService.Web.Storage;

namespace ChatService.Web.Services;

public class ProfileService : IProfileService
{
    private readonly IProfileStore _profileStore;
    private readonly IImageStore _imageStore;

    public ProfileService(IProfileStore profileStore, IImageStore imageStore)
    {
        _profileStore = profileStore;
        _imageStore = imageStore;
    }

    public async Task<Profile?> GetProfile(string username)
    {
        return await _profileStore.GetProfile(username);
    }
    
    public async Task AddProfile(Profile profile)
    {
        if (profile == null ||
            string.IsNullOrWhiteSpace(profile.username) ||
            string.IsNullOrWhiteSpace(profile.firstName) ||
            string.IsNullOrWhiteSpace(profile.lastName) ||
            string.IsNullOrWhiteSpace(profile.profilePictureId)
           )
        {
            throw new ArgumentException($"Invalid profile {profile}", nameof(profile));
        }

        bool imageExists = await _imageStore.ImageExists(profile.profilePictureId);
        if (!imageExists)
        {
            throw new ImageNotFoundException("Invalid profile picture ID.");
        }
        
        await _profileStore.AddProfile(profile);
    }

    public async Task DeleteProfile(string username)
    {
        Profile? profile = await GetProfile(username);
        if (profile == null)
        {
            throw new ArgumentException($"Profile with username {username} doesn't exist.");
        }
        await _imageStore.DeleteImage(profile.profilePictureId);
        await _profileStore.DeleteProfile(username);
    }
}