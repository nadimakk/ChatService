using ChatService.Web.Dtos;

namespace ChatService.Web.Storage;

public interface IProfileStore
{
    Task AddProfile(Profile profile);
    Task<Profile?> GetProfile(string username);
    Task DeleteProfile(string username);
    Task<bool> ProfileExists(string username);
}