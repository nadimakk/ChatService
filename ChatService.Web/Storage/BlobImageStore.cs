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

    public async Task<string> UploadImage(ImageDto image)
    {
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

    public async Task<ImageDto?> DownloadImage(string id)
    {
        BlobClient blobClient = BlobContainerClient.GetBlobClient(id);
        bool blobExists = await blobClient.ExistsAsync();
        if (!blobExists)
        {
            return null;
        }
        BlobProperties properties = await blobClient.GetPropertiesAsync();
        string contentType = properties.ContentType;
        
        MemoryStream content = new MemoryStream();
        await blobClient.DownloadToAsync(content);

        return new ImageDto(contentType, content);
    }

    public async Task DeleteImage(string id)
    {
        BlobClient blobClient = BlobContainerClient.GetBlobClient(id);
        await blobClient.DeleteIfExistsAsync();
    }
}