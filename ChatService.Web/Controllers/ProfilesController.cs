using ChatService.Web.Dtos;
using ChatService.Web.Exceptions;
using ChatService.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChatService.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProfilesController : ControllerBase
{
    private readonly IProfileService _profileService;
    private readonly ILogger<ProfilesController> _logger;

    public ProfilesController(IProfileService profileService, ILogger<ProfilesController> logger)
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
                _logger.LogInformation("Profile of {Username} fetched.", username);
                return Ok(profile);
            }
            catch (ProfileNotFoundException e)
            {
                _logger.LogError(e, "Error finding profile: {ErrorMessage}", e.Message);
                return NotFound(e.Message);
            }
        }
    }
    
    [HttpPost]
    public async Task<ActionResult<Profile>> PostProfile(Profile profile)
    {
        using (_logger.BeginScope("{Profile}", profile))
        {
            try
            {
                await _profileService.AddProfile(profile);
                _logger.LogInformation("Created Profile for user {ProfileUsername}.", profile.Username);
                return CreatedAtAction(nameof(GetProfile), new { username = profile.Username }, profile);
            }
            catch (Exception e) when (e is ArgumentException || e is ImageNotFoundException || e is InvalidUsernameException)
            {
                _logger.LogError(e, "Error posting profile: {ErrorMessage}", e.Message);
                return BadRequest(e.Message);
            }
            catch (UsernameTakenException e)
            {
                _logger.LogError(e, "Error posting profile: {ErrorMessage}", e.Message);
                return Conflict(e.Message);
            }   
        }
    }
}