using ChatService.Web.Dtos;

namespace ChatService.Web.Services;

public interface IProfileService
{
    Task<Profile?> GetProfile(string username);
    Task AddProfile(Profile profile);
    Task<bool> ProfileExists(string username);
    Task DeleteProfile(string username);
}