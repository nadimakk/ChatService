using ChatService.Web.Dtos;

namespace ChatService.Web.Storage;

public class BlobImageStore : IImageStore
{
    public Task UploadImage(UploadImageRequest request)
    {
        throw new NotImplementedException();
    }
    
    public void UploadImageToBlobStorage(string containerName, string blobName)
    {
        // Parse the connection string and create a CloudStorageAccount object
        CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connectionString);

        // Create the blob client object
        CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

        // Retrieve a reference to a container
        CloudBlobContainer container = blobClient.GetContainerReference(containerName);

        // Create the container if it does not exist
        container.CreateIfNotExists();

        // Retrieve a reference to a blob
        CloudBlockBlob blockBlob = container.GetBlockBlobReference(blobName);

        // Open the file and upload its data to the blob
        using (var fileStream = File.OpenRead(imagePath))
        {
            blockBlob.UploadFromStream(fileStream);
        }
    }

    public Task<MemoryStream> DownloadImage(string id)
    {
        throw new NotImplementedException();
    }
}