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
        
        string imageId = await _imageStore.UploadImage(request.File);
        return Ok(new UploadImageResponse(imageId));
    }
    
    [HttpGet("{id}")]
    public async  Task<IActionResult> DownloadImage(string id)
    {
        FileContentResult? imageResult = await _imageStore.DownloadImage(id);
        if (imageResult == null)
        {
           return NotFound($"An image with id {id} was not found.");
        }
        return imageResult;
    }
}