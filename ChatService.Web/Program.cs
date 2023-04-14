using Azure.Storage.Blobs;
using ChatService.Web.Configuration;
using ChatService.Web.Services;
using ChatService.Web.Storage;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<CosmosSettings>(builder.Configuration.GetSection("Cosmos"));
builder.Services.Configure<BlobSettings>(builder.Configuration.GetSection("BlobStorage"));

builder.Services.AddSingleton<IProfileStore, CosmosProfileStore>();
builder.Services.AddSingleton<IImageStore, BlobImageStore>();
builder.Services.AddSingleton<IUserConversationStore, CosmosUserConversationStore>();
builder.Services.AddSingleton<IMessageStore, CosmosMessageStore>();

builder.Services.AddSingleton(sp =>
{
    var cosmosOptions = sp.GetRequiredService<IOptions<CosmosSettings>>();
    return new CosmosClient(cosmosOptions.Value.ConnectionString);
});
builder.Services.AddSingleton(sp =>
    {
        var blobOptions = sp.GetRequiredService<IOptions<BlobSettings>>();
        return new BlobServiceClient(blobOptions.Value.ConnectionString);
    }
);
builder.Services.AddSingleton<IProfileService, ProfileService>();
builder.Services.AddSingleton<IImageService, ImageService>();
builder.Services.AddSingleton<IMessageService, MessageService>();
builder.Services.AddSingleton<IUserConversationService, UserConversationService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddApplicationInsightsTelemetry();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }