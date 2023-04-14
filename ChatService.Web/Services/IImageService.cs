using ChatService.Web.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace ChatService.Web.Services;

public interface IImageService
{
    Task<UploadImageServiceResult> UploadImage(Image image);
    Task<FileContentResult> DownloadImage(string imageId);
    Task DeleteImage(string imageId);
    Task<bool> ImageExists(string imageId);
}