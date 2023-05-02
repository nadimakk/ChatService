using ChatService.Web.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ChatService.Web.Tests.Dependencies;

public class DiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    
    public DiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }
    
    [Fact]
    public void AllDependenciesAreRegistered()
    {
        _factory.Services.GetRequiredService<IProfileService>();
        _factory.Services.GetRequiredService<IImageService>();
        _factory.Services.GetRequiredService<IConversationService>();
    }
}