using ChatService.Web.Dtos;

namespace ChatService.Web.Storage;

public interface IImageStore
{
    Task<string> UploadImage(Image image);
    Task<Image?> DownloadImage(string id);
    Task<bool> DeleteImage(string id);
    Task<bool> ImageExists(string id);
}