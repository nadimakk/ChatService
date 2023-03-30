using ChatService.Web.Dtos;
using ChatService.Web.Services;
using ChatService.Web.Storage;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace ChatService.Web.Tests.Services;

public class ConversationServiceTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly Mock<IMessageService> _messageServiceMock = new();
    private readonly Mock<IConversationStore> _conversationStoreMock = new();
    private readonly Mock<IProfileService> _profileServiceMock = new();

    private readonly IConversationService _conversationService;

    private readonly List<string> participants = new List<string>
    {
        Guid.NewGuid().ToString(),
        Guid.NewGuid().ToString()
    };
    
    private readonly SendMessageRequest = new SendMessageRequest
    {
        
    };

    private readonly StartConversationRequest _startConversationRequest = new StartConversationRequest
    {
        participants = participants,
        
    };
    
    public ConversationServiceTests(WebApplicationFactory<Program> factory)
    {
        _conversationService = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(_messageServiceMock.Object);
                services.AddSingleton(_conversationStoreMock.Object);
                services.AddSingleton(_profileServiceMock.Object);
            });
        }).Services.GetRequiredService<IConversationService>();
    }

    [Fact]
    public async Task CreateConversation_Success()
    {
        _profileServiceMock.Setup(m => m.ProfileExists())
        
        _profileStoreMock.Setup(m => m.GetProfile(_profile.username))
            .ReturnsAsync(_profile);
        
        
    }
}