using ChatService.Web.Dtos;

namespace ChatService.Web.Storage;

public interface IImageStore
{
    Task<string> UploadImage(ImageDto image);
    Task<ImageDto?> DownloadImage(string id);
    Task DeleteImage(string id);
}