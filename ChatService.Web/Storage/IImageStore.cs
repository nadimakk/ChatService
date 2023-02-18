using ChatService.Web.Dtos;

namespace ChatService.Web.Storage;

public interface IImageStore
{
    Task UploadImage(UploadImageRequest request);
    Task<MemoryStream> DownloadImage(string id);
 
}