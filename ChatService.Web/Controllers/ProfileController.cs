using ChatService.Web.Dtos;
using ChatService.Web.Exceptions;
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
        catch (Exception e) when (e is ArgumentException || e is ImageNotFoundException || e is InvalidUsernameException)
        {
            return BadRequest(e.Message);
        }
        catch (UsernameTakenException e)
        {
            return Conflict(e.Message);
        }
    }
}