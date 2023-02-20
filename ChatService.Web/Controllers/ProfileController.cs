using ChatService.Web.Dtos;
using ChatService.Web.Storage;
using Microsoft.AspNetCore.Mvc;

namespace ChatService.Web.Controllers;

[ApiController]
[Route("[controller]")]
public class ProfileController : ControllerBase
{
    private readonly IProfileStore _profileStore;

    public ProfileController(IProfileStore profileStore)
    {
        _profileStore = profileStore;
    }

    [HttpGet("{username}")]
    public async Task<ActionResult<Profile>> GetProfile(string username)
    {
        var profile = await _profileStore.GetProfile(username);
        if (profile == null)
        {
            return NotFound($"A profile with the username {username} was not found.");
        }

        return Ok(profile);
    }
    
    [HttpPost]
    public async Task<ActionResult<Profile>> PostProfile(Profile profile)
    {
        var isUsernameTaken = await _profileStore.GetProfile(profile.username) != null;
        if (isUsernameTaken)
        {
            return Conflict($"A user with the username {profile.username} already exist.");
        }

        await _profileStore.AddProfile(profile);
        return CreatedAtAction(nameof(GetProfile), new { username = profile.username }, profile);
    }
}