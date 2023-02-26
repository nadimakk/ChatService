using ChatService.Web.Dtos;
using ChatService.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChatService.Web.Controllers;

[ApiController]
[Route("[controller]")]
public class ProfileController : ControllerBase
{
    private readonly IProfileService _profileService;
    
    public ProfileController(IProfileService profileService)
    {
        _profileService = profileService;
    }

    [HttpGet("{username}")]
    public async Task<ActionResult<Profile>> GetProfile(string username)
    {
        var profile = await _profileService.GetProfile(username);
        if (profile == null)
        {
            return NotFound($"A profile with the username {username} was not found.");
        }

        return Ok(profile);
    }
    
    [HttpPost]
    public async Task<ActionResult<Profile>> PostProfile(Profile profile)
    {
        try
        {
            await _profileService.AddProfile(profile);
            return CreatedAtAction(nameof(GetProfile), new { username = profile.username }, profile);
        }
        catch (ArgumentException e)
        {
            if (e.Message == $"The username {profile.username} is taken.")
            {
                return Conflict(e.Message);
            }
            if (e.Message == $"Invalid profile {profile}" || e.Message == "Invalid profile picture ID.")
            {
                return BadRequest(e.Message);
            }
            throw;
        }
    }
}