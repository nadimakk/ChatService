using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Mvc;

namespace ChatService.Web.Storage;

public class BlobImageStore : IImageStore
{
    private readonly BlobServiceClient _blobServiceClient;

    public BlobImageStore(BlobServiceClient blobServiceClient)
    {
        _blobServiceClient = blobServiceClient;
    }

    private BlobContainerClient BlobContainerClient => _blobServiceClient.GetBlobContainerClient("images");

    public async Task<string> UploadImage(IFormFile file)
    {
        string imageId = Guid.NewGuid().ToString();
        BlobClient blobClient = BlobContainerClient.GetBlobClient(imageId);
        BlobHttpHeaders headers = new BlobHttpHeaders
        {
            ContentType = file.ContentType
        };
        await blobClient.UploadAsync(file.OpenReadStream(), headers);
        return imageId;
    }

    public async Task<FileContentResult?> DownloadImage(string id)
    {
        BlobClient blobClient = BlobContainerClient.GetBlobClient(id);
        bool blobExists = await blobClient.ExistsAsync();
        if (!blobExists)
        {
            return null;
        }
        BlobProperties properties = await blobClient.GetPropertiesAsync();
        string contentType = properties.ContentType;
        
        MemoryStream stream = new MemoryStream();
        await blobClient.DownloadToAsync(stream);
        byte[] blobContent = stream.ToArray();
        
        return new FileContentResult(blobContent, contentType);
    }
}