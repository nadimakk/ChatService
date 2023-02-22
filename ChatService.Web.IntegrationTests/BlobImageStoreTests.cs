using System.Text;
using ChatService.Web.Dtos;
using ChatService.Web.Storage;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ChatService.Web.IntegrationTests;

public class BlobImageStoreTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{ 
    private readonly IImageStore _store;
    private readonly Image _image = new Image("image/jpg",
        new MemoryStream(Encoding.UTF8.GetBytes("This is a mock image file content")));
    private string _imageId;
    
    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _store.DeleteImage(_imageId);
    }
    
    public BlobImageStoreTests(WebApplicationFactory<Program> factory)
    {
        _store = factory.Services.GetRequiredService<IImageStore>();
    }

    [Fact]
    public async Task UploadImage_Success()
    {
        string imageId = await _store.UploadImage(_image);
        var downloadedImage = await _store.DownloadImage(imageId);

        Assert.Equal(_image.ContentType, downloadedImage.ContentType);
        Assert.True(_image.Content.ToArray().SequenceEqual(downloadedImage.Content.ToArray()));
        
        _imageId = imageId;
    }

    [Fact]
    public async Task UploadImage_Failure()
    {
       var notImage = new Image("text/plain",
            new MemoryStream(Encoding.UTF8.GetBytes("This is a mock file simulating an invalid image type")));
       
       await Assert.ThrowsAsync<ArgumentException>(async () => await _store.UploadImage(notImage));
    }

    [Fact]
    public async Task DownloadImage_Success()
    {
        var imageId = await _store.UploadImage(_image);
        var downloadedImage = await _store.DownloadImage(imageId);
         
        Assert.Equal(_image.ContentType, downloadedImage.ContentType);
        Assert.True(_image.Content.ToArray().SequenceEqual(downloadedImage.Content.ToArray()));
    }

    [Fact]
    public async Task DownloadImage_NotFound()
    {
        var downloadedImage = await _store.DownloadImage("dummy_id");
        
        Assert.Null(downloadedImage);
    }
    
    [Fact]
    public async Task DeleteImage_Success()
    {
        var imageId = await _store.UploadImage(_image);
        Assert.True(await _store.DeleteImage(imageId));
    }
    
    [Fact]
    public async Task DeleteImage_Failure()
    {
        Assert.False(await _store.DeleteImage("dummy_id"));
    }
}