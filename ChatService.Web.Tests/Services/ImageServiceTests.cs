using ChatService.Web.Dtos;
using ChatService.Web.Exceptions;
using ChatService.Web.Services;
using ChatService.Web.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace ChatService.Web.Tests.Services;

public class ImageServiceTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly Mock<IImageStore> _imageStoreMock = new();
    private readonly IImageService _imageService;

    private readonly string _imageId = Guid.NewGuid().ToString();
    private readonly Image _image = new(
        ContentType: "image/jpeg", 
        Content: new MemoryStream(new byte[] { 0x01, 0x02, 0x03 }));
    
    public ImageServiceTests(WebApplicationFactory<Program> factory)
    {
        _imageService = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(_imageStoreMock.Object);
            });
        }).Services.GetRequiredService<IImageService>();
    }

    [Fact]
    public async Task UploadImage_Success()
    {
        _imageStoreMock.Setup(m => m.UploadImage(It.IsAny<Image>()))
            .ReturnsAsync(_imageId);

        var expectedUploadImageServiceResult = new UploadImageResult(_imageId);
        
        var receivedUploadImageServiceResult = await _imageService.UploadImage(_image);
        
        Assert.Equal(expectedUploadImageServiceResult, receivedUploadImageServiceResult);
    }

    // [Fact]
    // public async Task UploadImage_InvalidImageType()
    // {
    //     var invalidImage = new Image(ContentType: "text/plain", Content: new MemoryStream());
    //
    //     await Assert.ThrowsAsync<InvalidImageTypeException>(() => _imageService.UploadImage(invalidImage));
    //     
    //     _imageStoreMock.Verify(m => m.UploadImage(It.IsAny<Image>()), Times.Never);
    // }
    
    [Fact]
    public async Task DownloadImage_Success()
    {
        _imageStoreMock.Setup(m => m.DownloadImage(_imageId))
            .ReturnsAsync(_image);

        var receivedImage = await _imageService.DownloadImage(_imageId);
        
        Assert.Equal(_image.ContentType, receivedImage.ContentType);
        Assert.True(_image.Content.ToArray().SequenceEqual(receivedImage.Content.ToArray()));
    }
    
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task DownloadImage_InvalidArguments(string imageId)
    {

        await Assert.ThrowsAsync<ArgumentException>(() => _imageService.DownloadImage(imageId));
    }
    
    [Fact]
    public async Task DownloadImage_ImageNotFound()
    {
        _imageStoreMock.Setup(m => m.DownloadImage(_imageId))
            .ReturnsAsync((Image?)null);
        
        await Assert.ThrowsAsync<ImageNotFoundException>(() => _imageService.DownloadImage(_imageId));
    }
}