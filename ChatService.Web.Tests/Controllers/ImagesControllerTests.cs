using System.Net;
using System.Net.Http.Headers;
using System.Text;
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
    private readonly MultipartFormDataContent _content = new();
    
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
        var image = new Image("image/jpeg", new MemoryStream());
        var imageId = Guid.NewGuid().ToString();
        var uploadImageResponse = new UploadImageResponse(imageId);
        
        _imageServiceMock.Setup(m => m.UploadImage(It.IsAny<Image>()))
            .ReturnsAsync(new UploadImageServiceResult(imageId));
        
        var fileContent = new StreamContent(image.Content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(image.ContentType);
        _content.Add(fileContent,"File", "image.jpeg");
        
        var response = await _httpClient.PostAsync("api/Images/", _content);
        
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        
        Assert.Equal($"http://localhost/api/Images/{imageId}", response.Headers.GetValues("Location").First());

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
    public async Task UploadImage_InvalidFile()
    {
        var content = new MemoryStream(Encoding.UTF8.GetBytes("This is a mock text file content"));
        var contentType = "text/plain";
        var fileContent = new StreamContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        _content.Add(fileContent,"File", "file.txt");
        
        _imageServiceMock.Setup(m => m.UploadImage(It.IsAny<Image>()))
            .ThrowsAsync(new InvalidImageTypeException($"Invalid image type {contentType}."));
        
        var response = await _httpClient.PostAsync("api/Images/", _content);
        
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        Assert.Equal($"Invalid image type {contentType}.", json);
    }
    
    [Fact]
    public async Task DownloadImage_Success()
    {
        var imageId = Guid.NewGuid().ToString();
        var image = new Image("image/jpeg", new MemoryStream());
        var fileContentResult = new FileContentResult(image.Content.ToArray(), image.ContentType);

        _imageServiceMock.Setup(m => m.DownloadImage(imageId))
            .ReturnsAsync(new FileContentResult(image.Content.ToArray(), image.ContentType));

        var response = await _httpClient.GetAsync($"api/Images/{imageId}");
        
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var content = await response.Content.ReadAsByteArrayAsync();
        var contentType = response.Content.Headers.ContentType.ToString();
        
        Assert.Equal(fileContentResult.FileContents, content);
        Assert.Equal(fileContentResult.ContentType, contentType);
    }
   
    [Fact]
    public async Task DownloadImage_NotFound()
    {
        var imageId = Guid.NewGuid().ToString();

        _imageServiceMock.Setup(m => m.DownloadImage(imageId))
            .ThrowsAsync( new ImageNotFoundException($"An image with id {imageId} was not found."));
        
        var response = await _httpClient.GetAsync($"api/Images/{imageId}");
        
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
    
    [Fact]
    public async Task DownloadImage_InvalidArgument()
    {
        var imageId = Guid.NewGuid().ToString();

        _imageServiceMock.Setup(m => m.DownloadImage(imageId))
            .ThrowsAsync( new ArgumentException("Invalid imageId"));
        
        var response = await _httpClient.GetAsync($"api/Images/{imageId}");
        
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}