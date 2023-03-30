using System.Net;
using ChatService.Web.Dtos;
using ChatService.Web.Exceptions;
using ChatService.Web.Storage.Entities;
using Microsoft.Azure.Cosmos;

namespace ChatService.Web.Storage;

public class CosmosProfileStore : IProfileStore
{
    private readonly CosmosClient _cosmosClient;

    public CosmosProfileStore(CosmosClient cosmosClient)
    {
        _cosmosClient = cosmosClient;
    }

    private Container Container => _cosmosClient.GetDatabase("chatService").GetContainer("sharedContainer");

    public async Task AddProfile(Profile profile)
    {
        if (profile == null ||
            string.IsNullOrWhiteSpace(profile.username) ||
            string.IsNullOrWhiteSpace(profile.firstName) ||
            string.IsNullOrWhiteSpace(profile.lastName) ||
            string.IsNullOrWhiteSpace(profile.profilePictureId)
           )
        {
            throw new ArgumentException($"Invalid profile {profile}", nameof(profile));
        }

        try
        {
            await Container.CreateItemAsync(ToEntity(profile), new PartitionKey(profile.username));
        }
        catch (CosmosException e)
        {
            if (e.StatusCode == HttpStatusCode.Conflict)
            {
                throw new UsernameTakenException($"A profile with username {profile.username} already exists.");
            }
            throw;
        }
    }
    
    public async Task<Profile?> GetProfile(string username)
    {
        try
        {
            var entity = await Container.ReadItemAsync<ProfileEntity>(
                id: username,
                partitionKey: new PartitionKey(username),
                new ItemRequestOptions
                {
                    ConsistencyLevel = ConsistencyLevel.Session
                }
            );
            return ToProfile(entity);
        }
        catch (CosmosException e)
        {
            if (e.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
            throw;
        }
    }

    public async Task DeleteProfile(string username)
    {
        try
        {
            await Container.DeleteItemAsync<Profile>(
                id: username, 
                partitionKey: new PartitionKey(username));
        }
        catch (CosmosException e)
        {
            if (e.StatusCode == HttpStatusCode.NotFound)
            {
                return;
            }
            throw;
        }
    }

    public async Task<bool> ProfileExists(string username)
    {
        Profile profile = await GetProfile(username);

        return profile != null;
    }

    private static ProfileEntity ToEntity(Profile profile)
    {
        return new ProfileEntity(
            partitionKey: profile.username,
            id: profile.username,
            profile.firstName,
            profile.lastName,
            profile.profilePictureId
        );
    }

    private static Profile ToProfile(ProfileEntity entity)
    {
        return new Profile(
            username: entity.id,
            entity.firstName,
            entity.lastName,
            entity.profilePictureId
        );
    }
}