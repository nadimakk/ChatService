using ChatService.Web.Dtos;
using ChatService.Web.Storage;
using Microsoft.AspNetCore.Mvc;

namespace ChatService.Web.Services;

public class ImageService : IImageService
{
    private readonly IImageStore _imageStore;

    public ImageService(IImageStore imageStore)
    {
        _imageStore = imageStore;
    }

    public Task<UploadImageServiceResult> UploadImage(Image image)
    {
        throw new NotImplementedException();
    }

    public Task<FileContentResult> DownloadImage(string imageId)
    {
        throw new NotImplementedException();
    }
}