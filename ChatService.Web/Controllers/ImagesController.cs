using ChatService.Web.Dtos;
using ChatService.Web.Exceptions;
using ChatService.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChatService.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ImagesController : ControllerBase
{
    private readonly IImageService _imageService;
    private readonly ILogger<ImagesController> _logger;

    public ImagesController(IImageService imageService, ILogger<ImagesController> logger)
    {
        _imageService = imageService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<UploadImageResponse>> UploadImage([FromForm] UploadImageRequest request)
    {
        MemoryStream content = new();
        await request.File.CopyToAsync(content);
        Image image = new Image(request.File.ContentType, content);
        
        try
        {
             UploadImageServiceResult result = await _imageService.UploadImage(image);
             _logger.LogInformation("Uploaded image with id {id}.", result.ImageId);
             return CreatedAtAction(nameof(DownloadImage), new { imageId = result.ImageId }, 
                 new UploadImageResponse(result.ImageId));
        }
        catch (InvalidImageTypeException e)
        {
            _logger.LogError(e, "Error uploading image: {ErrorMessage}", e.Message);
            return BadRequest(e.Message);
        }
    }
    
    [HttpGet("{imageId}")] 
    public async  Task<IActionResult> DownloadImage(string imageId)
    {
        using (_logger.BeginScope("{ImageId}", imageId))
        {
            try
            {
                var result = await _imageService.DownloadImage(imageId);
                _logger.LogInformation("Downloaded image with id {id}.", imageId);
                return result;
            }
            catch (ArgumentException e)
            {
                _logger.LogError(e, "Error downloading image: {ErrorMessage}", e.Message);
                return BadRequest(e.Message);
            }
            catch (ImageNotFoundException e)
            {
                _logger.LogError(e, "Error downloading image: {ErrorMessage}", e.Message);
                return NotFound(e.Message);
            }
        }
    }
}