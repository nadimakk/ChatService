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

    public ImagesController(IImageService imageService)
    {
        _imageService = imageService;
    }

    [HttpPost]
    public async Task<ActionResult<UploadImageResponse>> UploadImage([FromForm] UploadImageRequest request)
    {
        MemoryStream content = new();
        await request.File.CopyToAsync(content);
        Image image = new Image(request.File.ContentType, content);

        UploadImageServiceResult result;
        
        try
        {
             result = await _imageService.UploadImage(image);
        }
        catch (InvalidImageTypeException e)
        {
            return BadRequest(e.Message);
        }
        
        return CreatedAtAction(nameof(DownloadImage), new { imageId = result.ImageId }, 
            new UploadImageResponse(result.ImageId));
    }
    
    [HttpGet("{imageId}")] 
    public async  Task<IActionResult> DownloadImage(string imageId)
    {
        try
        {
            return await _imageService.DownloadImage(imageId);
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