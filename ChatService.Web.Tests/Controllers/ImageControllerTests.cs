using System.Net;
using ChatService.Web.Dtos;
using ChatService.Web.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json;

namespace ChatService.Web.Tests.Controllers;

public class ImageControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly Mock<IImageStore> _imageStoreMock = new();
    private readonly Mock<IFormFile> _formFileMock = new();
    private readonly HttpClient _httpClient;

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
        var imageId = Guid.NewGuid().ToString();
        var uploadImageResponse = new UploadImageResponse(imageId);

        _formFileMock.Setup(m => m.ContentType)
            .Returns("image/jpg");
        _formFileMock.Setup(m => m.OpenReadStream())
            .Returns(new MemoryStream());
        _imageStoreMock.Setup(m => m.UploadImage(_formFileMock.Object))
            .ReturnsAsync(imageId);

        var content = new MultipartFormDataContent();
        var fileContent = new StreamContent(_formFileMock.Object.OpenReadStream());
        content.Add(fileContent,"File");
        
        var response = await _httpClient.PostAsync("/Image", content);
        
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var json = await response.Content.ReadAsStringAsync();
        var receivedUploadImageResponse = JsonConvert.DeserializeObject<UploadImageResponse>(json);
        Assert.Equal(uploadImageResponse, receivedUploadImageResponse);
    }
    
}