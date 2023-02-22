using ChatService.Web.Dtos;
using ChatService.Web.Storage;
using Microsoft.AspNetCore.Mvc;

namespace ChatService.Web.Controllers;

[ApiController]
[Route("[controller]")]
public class ImageController : ControllerBase
{
    private readonly IImageStore _imageStore;

    public ImageController(IImageStore imageStore)
    {
        _imageStore = imageStore;
    }

    [HttpPost]
    public async Task<ActionResult<UploadImageResponse>> UploadImage([FromForm] UploadImageRequest request)
    {
        string contentType = request.File.ContentType.ToLower();
        if (contentType != "image/jpg" &&
            contentType != "image/jpeg" &&
            contentType != "image/png")
        {
            return BadRequest($"Invalid file, must be an image.");
        }

        MemoryStream content = new();
        await request.File.CopyToAsync(content);
        Image image = new Image(contentType, content);
        
        string imageId = await _imageStore.UploadImage(image);
        return CreatedAtAction(nameof(DownloadImage), new { id = imageId }, new UploadImageResponse(imageId));
    }
    
    [HttpGet("{id}")] 
    public async  Task<IActionResult> DownloadImage(string id)
    {
        Image? image = await _imageStore.DownloadImage(id);
        if (image == null)
        {
           return NotFound($"An image with id {id} was not found.");
        }
        return new FileContentResult(image.Content.ToArray(), image.ContentType);
    }
}