using System.Net;
using System.Net.Http.Headers;
using ChatService.Web.Dtos;
using ChatService.Web.Exceptions;
using ChatService.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json;

namespace ChatService.Web.Tests.Controllers;

public class ImagesControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly Mock<IImageService> _imageServiceMock = new();
    private readonly HttpClient _httpClient;
    
    private static readonly Image _image = new(ContentType: "image/jpeg", Content: new MemoryStream());
    
    private readonly MultipartFormDataContent _content = new();
    
    private readonly StreamContent _fileContent = new(_image.Content)
    {
        Headers = { ContentType = new MediaTypeHeaderValue(_image.ContentType) }
    };
    
    private readonly string _imageId = Guid.NewGuid().ToString();
    
    public ImagesControllerTests(WebApplicationFactory<Program> factory)
    {
        _httpClient = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services => { services.AddSingleton(_imageServiceMock.Object); });
        }).CreateClient();
    }

    [Fact]
    public async Task UploadImage_Success()
    {
        var uploadImageResponse = new UploadImageResponse(_imageId);
        
        _imageServiceMock.Setup(m => m.UploadImage(It.IsAny<Image>()))
            .ReturnsAsync(new UploadImageResult(_imageId));
        
        _content.Add(_fileContent,"File", "image.jpeg");
        
        var response = await _httpClient.PostAsync("api/Images/", _content);
        
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        Assert.Equal($"http://localhost/api/Images/{_imageId}", response.Headers.GetValues("Location").First());

        var json = await response.Content.ReadAsStringAsync();
        var receivedUploadImageResponse = JsonConvert.DeserializeObject<UploadImageResponse>(json);
        
        Assert.Equal(uploadImageResponse, receivedUploadImageResponse);
    }
    
    [Fact]
    public async Task UploadImage_MissingFile()
    {
        var response = await _httpClient.PostAsync("api/Images/", _content);
        
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        
        _imageServiceMock.Verify( m => m.UploadImage(It.IsAny<Image>()), Times.Never);
    }
    
    [Fact]
    public async Task UploadImage_InvalidImageType()
    {
        _imageServiceMock.Setup(m => m.UploadImage(It.IsAny<Image>()))
            .ThrowsAsync(new InvalidImageTypeException($"Invalid image type {_image.ContentType}."));
        
        _content.Add(_fileContent,"File", "text/plain");
        
        var response = await _httpClient.PostAsync("api/Images/", _content);
        
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    
    [Fact]
    public async Task UploadImage_BlobServiceUnavailable()
    {
        _imageServiceMock.Setup(m => m.UploadImage(It.IsAny<Image>()))
            .ThrowsAsync(new BlobServiceUnavailableException("Blob service is unavailable."));
        _content.Add(_fileContent,"File", "image.jpeg");

        var response = await _httpClient.PostAsync("api/Images/", _content);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
    
    [Fact]
    public async Task DownloadImage_Success()
    {
        var fileContentResult = new FileContentResult(_image.Content.ToArray(), _image.ContentType);

        _imageServiceMock.Setup(m => m.DownloadImage(_imageId))
            .ReturnsAsync(_image);

        var response = await _httpClient.GetAsync($"api/Images/{_imageId}");
        
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Content.Headers.ContentType);
        
        var contentType = response.Content.Headers.ContentType.ToString();
        var content = await response.Content.ReadAsByteArrayAsync();
        
        Assert.Equal(fileContentResult.FileContents, content);
        Assert.Equal(fileContentResult.ContentType, contentType);
    }
   
    [Fact]
    public async Task DownloadImage_NotFound()
    {
        _imageServiceMock.Setup(m => m.DownloadImage(_imageId))
            .ThrowsAsync( new ImageNotFoundException($"An image with id {_imageId} was not found."));
        
        var response = await _httpClient.GetAsync($"api/Images/{_imageId}");
        
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
    
    [Fact]
    public async Task DownloadImage_InvalidArgument()
    {
        _imageServiceMock.Setup(m => m.DownloadImage(_imageId))
            .ThrowsAsync( new ArgumentException("Invalid imageId"));
        
        var response = await _httpClient.GetAsync($"api/Images/{_imageId}");
        
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    
    [Fact]
    public async Task DownloadImage_BlobServiceUnavailable()
    {
        _imageServiceMock.Setup(m => m.DownloadImage(_imageId))
            .ThrowsAsync(new BlobServiceUnavailableException("Blob service is unavailable."));

        var response = await _httpClient.GetAsync($"api/Images/{_imageId}");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}