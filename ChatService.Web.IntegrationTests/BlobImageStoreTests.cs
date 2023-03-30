using System.Text;
using Azure;
using ChatService.Web.Dtos;
using ChatService.Web.Storage;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ChatService.Web.IntegrationTests;

public class BlobImageStoreTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{ 
    private readonly IImageStore _imageStore;
    private readonly Image _image = new Image("image/jpg",
        new MemoryStream(Encoding.UTF8.GetBytes("This is a mock image file content")));
    private string _imageId;
    
    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _imageStore.DeleteImage(_imageId);
    }
    
    public BlobImageStoreTests(WebApplicationFactory<Program> factory)
    {
        _imageStore = factory.Services.GetRequiredService<IImageStore>();
    }

    [Fact]
    public async Task UploadImage_Success()
    {
        string imageId = await _imageStore.UploadImage(_image);
        var downloadedImage = await _imageStore.DownloadImage(imageId);

        Assert.Equal(_image.ContentType, downloadedImage.ContentType);
        Assert.True(_image.Content.ToArray().SequenceEqual(downloadedImage.Content.ToArray()));
        
        _imageId = imageId;
    }

    [Fact]
    public async Task UploadImage_Failure()
    {
       var notImage = new Image("text/plain",
            new MemoryStream(Encoding.UTF8.GetBytes("This is a mock file simulating an invalid image type")));
       
       await Assert.ThrowsAsync<ArgumentException>(async () => await _imageStore.UploadImage(notImage));
    }

    [Fact]
    public async Task DownloadImage_Success()
    {
        var imageId = await _imageStore.UploadImage(_image);
        var downloadedImage = await _imageStore.DownloadImage(imageId);
         
        Assert.Equal(_image.ContentType, downloadedImage.ContentType);
        Assert.True(_image.Content.ToArray().SequenceEqual(downloadedImage.Content.ToArray()));
    }

    [Fact]
    public async Task DownloadImage_NotFound()
    {
        var downloadedImage = await _imageStore.DownloadImage("dummy_id");

        Assert.Null(downloadedImage);
    }
    
    [Fact]
    public async Task DeleteImage_Success()
    {
        var imageId = await _imageStore.UploadImage(_image);
        Assert.True(await _imageStore.DeleteImage(imageId));
    }
    
    [Fact]
    public async Task DeleteImage_Failure()
    {
        Assert.False(await _imageStore.DeleteImage("dummy_id"));
    }
    
    [Fact]
    public async Task ImageExists_Exists()
    {
        string imageId = await _imageStore.UploadImage(_image);
        
        Assert.True(await _imageStore.ImageExists(imageId));
        
        _imageId = imageId;
    }
        
    [Fact]
    public async Task ImageExists_DoesntExist()
    {
        Assert.False(await _imageStore.ImageExists("dummy_id"));
    }
}