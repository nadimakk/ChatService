using System.Text;
using ChatService.Web.Dtos;
using ChatService.Web.Storage;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ChatService.Web.IntegrationTests;

public class ImageStoreIntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{ 
    private readonly IImageStore _imageStore;
    private readonly Image _image = new (
        ContentType: "image/jpg", 
        Content: new MemoryStream(Encoding.UTF8.GetBytes("This is a mock image file content")));
    private string _imageId;
    
    public ImageStoreIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _imageStore = factory.Services.GetRequiredService<IImageStore>();
    }

    [Fact]
    public async Task UploadDownloadImage_Success()
    {
        _imageId = await _imageStore.UploadImage(_image);
        var downloadedImage = await _imageStore.DownloadImage(_imageId);

        Assert.Equal(_image.ContentType, downloadedImage.ContentType);
        Assert.True(_image.Content.ToArray().SequenceEqual(downloadedImage.Content.ToArray()));
    }

    [Fact]
    public async Task UploadImage_WrongContentType()
    {
       var notImage = new Image(
           ContentType: "text/plain",
           Content: new MemoryStream(Encoding.UTF8.GetBytes("This is a mock file simulating an invalid image type")));
       await Assert.ThrowsAsync<ArgumentException>(async () => await _imageStore.UploadImage(notImage));
    }
    
    [Fact]
    public async Task DownloadImage_NonExistingImage()
    {
        var downloadedImage = await _imageStore.DownloadImage("dummy_id");
        Assert.Null(downloadedImage);
    }
    
    [Fact]
    public async Task DeleteImage_Success()
    {
        _imageId = await _imageStore.UploadImage(_image);
        var imageDeleted = await _imageStore.DeleteImage(_imageId);
        var downloadedImage = await _imageStore.DownloadImage(_imageId);
        
        Assert.True(imageDeleted);
        Assert.Null(downloadedImage);
    }
    
    [Fact]
    public async Task DeleteImage_NonExistingImage()
    {
        Assert.False(await _imageStore.DeleteImage("dummy_id"));
    }
    
    [Fact]
    public async Task ImageExists_Exists()
    {
        _imageId = await _imageStore.UploadImage(_image);
        Assert.True(await _imageStore.ImageExists(_imageId));
    }
        
    [Fact]
    public async Task ImageExists_DoesntExist()
    {
        Assert.False(await _imageStore.ImageExists("dummy_id"));
    }
    
    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _imageStore.DeleteImage(_imageId);
    }
}