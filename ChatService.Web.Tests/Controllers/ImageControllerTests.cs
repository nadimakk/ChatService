using System.Net;
using System.Net.Http.Headers;
using System.Text;
using ChatService.Web.Dtos;
using ChatService.Web.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json;

namespace ChatService.Web.Tests.Controllers;

public class ImageControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly Mock<IImageStore> _imageStoreMock = new();
    private readonly HttpClient _httpClient;
    private readonly MultipartFormDataContent _content = new();
    
    public ImageControllerTests(WebApplicationFactory<Program> factory)
    {
        _httpClient = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services => { services.AddSingleton(_imageStoreMock.Object); });
        }).CreateClient();
    }

    [Fact]
    public async Task UploadImage_Success()
    {
        var image = new Image("image/jpeg", new MemoryStream());
        var imageId = Guid.NewGuid().ToString();
        var uploadImageResponse = new UploadImageResponse(imageId);
        
        _imageStoreMock.Setup(m => m.UploadImage(It.IsAny<Image>()))
            .ReturnsAsync(imageId);
        
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
        
        _imageStoreMock.Verify( m => m.UploadImage(It.IsAny<Image>()), Times.Never);
    }
    
    [Fact]
    public async Task UploadImage_InvalidFile()
    {
        var fileContent = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes("This is a mock text file content")));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        _content.Add(fileContent,"File", "file.txt");
        
        var response = await _httpClient.PostAsync("api/Images/", _content);
        
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        Assert.Equal("Invalid file, must be an image.", json);
        
        _imageStoreMock.Verify( m => m.UploadImage(It.IsAny<Image >()), Times.Never);
    }
    
    [Fact]
    public async Task DownloadImage_Success()
    {
        var imageId = Guid.NewGuid().ToString();
        var image = new Image("image/jpeg", new MemoryStream());
        var fileContentResult = new FileContentResult(image.Content.ToArray(), image.ContentType);

        _imageStoreMock.Setup(m => m.DownloadImage(imageId))
            .ReturnsAsync(image);

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
        
        var response = await _httpClient.GetAsync($"api/Images/{imageId}");
        
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}