using System.Text;
using ChatService.Web.Dtos;
using ChatService.Web.Storage;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ChatService.Web.IntegrationTests;

public class BlobImageStoreTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{ 
    private readonly IImageStore _store;
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
        var image = new ImageDto("image/jpg", new MemoryStream(Encoding.UTF8.GetBytes("This is a mock image file content")));
        string imageId = await _store.UploadImage(image);
        var receivedImage = await _store.DownloadImage(imageId);

        Assert.Equal(image.ContentType, receivedImage.ContentType);
        Assert.True(image.Content.ToArray().SequenceEqual(receivedImage.Content.ToArray()));
        
        _imageId = imageId;
    }
}