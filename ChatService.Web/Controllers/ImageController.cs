using ChatService.Web.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace ChatService.Web.Controllers;

public class ImageController : ControllerBase
{
    public Task UploadImage(UploadImageRequest request)
    {
        throw new NotImplementedException();
    }

    public Task<MemoryStream> DownloadImage(string id)
    {
        throw new NotImplementedException();
    }
}