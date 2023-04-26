using ChatService.Web.Dtos;
using ChatService.Web.Exceptions;
using ChatService.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChatService.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProfileController : ControllerBase
{
    private readonly IProfileService _profileService;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(IProfileService profileService, ILogger<ProfileController> logger)
    {
        _profileService = profileService;
        _logger = logger;

    }

    [HttpGet("{username}")]
    public async Task<ActionResult<Profile>> GetProfile(string username)
    {
        using (_logger.BeginScope("{Username}", username))
        {
            try
            {
                var profile = await _profileService.GetProfile(username);
                return Ok(profile);
            }
            catch (UserNotFoundException e)
            {
                return NotFound(e.Message);
            }
            catch (ThirdPartyServiceUnavailableException e)
            {
                return new ObjectResult(e.Message) { StatusCode = 503 };
            }
        }
    }
    
    [HttpPost]
    public async Task<ActionResult<Profile>> PostProfile(Profile profile)
    {
        using (_logger.BeginScope("{Username}", profile.Username))
        {
            try
            {
                await _profileService.AddProfile(profile);
                _logger.LogInformation("Created Profile for user {ProfileUsername}.", profile.Username);
                return CreatedAtAction(nameof(GetProfile), new { username = profile.Username }, profile);
            }
            catch (Exception e) when (e is ArgumentException or ImageNotFoundException || e is InvalidUsernameException)
            {
                return BadRequest(e.Message);
            }
            catch (UsernameTakenException e)
            {
                _logger.LogError(e, "Error posting profile: {ErrorMessage}", e.Message);
                return Conflict(e.Message);
            }
            catch (ThirdPartyServiceUnavailableException e)
            {
                return new ObjectResult(e.Message) { StatusCode = 503 };
            }
        }
    }
}