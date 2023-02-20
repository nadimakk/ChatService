using Microsoft.AspNetCore.Mvc;

namespace ChatService.Web.Storage;

public interface IImageStore
{
    Task<string> UploadImage(IFormFile file);
    Task<FileContentResult?> DownloadImage(string id);
}