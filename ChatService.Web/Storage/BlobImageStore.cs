using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ChatService.Web.Dtos;

namespace ChatService.Web.Storage;

public class BlobImageStore : IImageStore
{
    private readonly BlobServiceClient _blobServiceClient;

    public BlobImageStore(BlobServiceClient blobServiceClient)
    {
        _blobServiceClient = blobServiceClient;
    }

    private BlobContainerClient BlobContainerClient => _blobServiceClient.GetBlobContainerClient("images");

    public async Task<string> UploadImage(Image image)
    {
        
        string contentType = image.ContentType.ToLower();
        if (contentType != "image/jpg" &&
            contentType != "image/jpeg" &&
            contentType != "image/png")
        {
            throw new ArgumentException("File type is not an image.");
        }
        
        string imageId = Guid.NewGuid().ToString();
        BlobClient blobClient = BlobContainerClient.GetBlobClient(imageId);
        BlobHttpHeaders headers = new BlobHttpHeaders
        {
            ContentType = image.ContentType
        };
        image.Content.Position = 0;
        await blobClient.UploadAsync(image.Content, headers);
        return imageId;
    }

    public async Task<Image?> DownloadImage(string id)
    {
        BlobClient blobClient = BlobContainerClient.GetBlobClient(id);

        try
        {
            MemoryStream content = new MemoryStream();
            await blobClient.DownloadToAsync(content);
            BlobProperties properties = await blobClient.GetPropertiesAsync();
            string contentType = properties.ContentType;
            return new Image(contentType, content);
        }
        catch (RequestFailedException ex)
        {
            if (ex.Status == 404)
            {
                return null;
            }
            throw;
        }
    }

    public async Task<bool> DeleteImage(string id)
    {
        BlobClient blobClient = BlobContainerClient.GetBlobClient(id);
        return await blobClient.DeleteIfExistsAsync();
    }

    public async Task<bool> ImageExists(string id)
    {
        BlobClient blobClient = BlobContainerClient.GetBlobClient(id);
        return await blobClient.ExistsAsync();
    }
}