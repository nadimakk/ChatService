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
        Image image = new(request.File.ContentType, content);
        
        try
        {
             UploadImageResult result = await _imageService.UploadImage(image);
             _logger.LogInformation("Uploaded image with id {id}.", result.ImageId);
             return CreatedAtAction(nameof(DownloadImage), new { imageId = result.ImageId }, 
                 new UploadImageResponse(result.ImageId));
        }
        catch (InvalidImageTypeException e)
        {
            return BadRequest(e.Message);
        }
    }
    
    [HttpGet("{imageId}")] 
    public async  Task<IActionResult> DownloadImage(string imageId)
    {
        try
        {
            Image image = await _imageService.DownloadImage(imageId);
            return new FileContentResult(image.Content.ToArray(), image.ContentType);
        }
        catch (ArgumentException e)
        {
            return BadRequest(e.Message);
        }
        catch (ImageNotFoundException e)
        {
            return NotFound(e.Message);
        }
    }
}