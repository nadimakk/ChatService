using ChatService.Web.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace ChatService.Web.Services;

public interface IImageService
{
    Task<UploadImageResult> UploadImage(Image image);
    Task<Image> DownloadImage(string imageId);
    Task DeleteImage(string imageId);
    Task<bool> ImageExists(string imageId);
}