using ChatService.Web.Dtos;
using ChatService.Web.Exceptions;
using ChatService.Web.Storage;

namespace ChatService.Web.Services;

public class ImageService : IImageService
{
    private readonly IImageStore _imageStore;

    public ImageService(IImageStore imageStore)
    {
        _imageStore = imageStore;
    }

    public async Task<UploadImageResult> UploadImage(Image image)
    {
        ValidateImage(image);
        
        string imageId = await _imageStore.UploadImage(image);
        
        return new UploadImageResult(imageId);
    }

    public async Task<Image> DownloadImage(string imageId)
    {
        ValidateImageId(imageId);
        
        Image? image = await _imageStore.DownloadImage(imageId);
        
        if (image == null)
        {
            throw new ImageNotFoundException($"An image with id {imageId} was not found.");
        }

        return image;
    }

    public async Task DeleteImage(string imageId)
    {
        await _imageStore.DeleteImage(imageId);
    }
    
    public async Task<bool> ImageExists(string imageId)
    {
        return await _imageStore.ImageExists(imageId);
    }

    private void ValidateImage(Image image)
    {
        string contentType = image.ContentType.ToLower();
        
        if (contentType != "image/jpg" &&
            contentType != "image/jpeg" &&
            contentType != "image/png")
        {
            throw new InvalidImageTypeException($"Invalid image type {contentType}.");
        }
    }
    
    private void ValidateImageId(string imageId)
    {
        if (string.IsNullOrWhiteSpace(imageId))
        {
            throw new ArgumentException("Invalid imageId");
        }
    }
}