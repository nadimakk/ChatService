using System.Net;
using Azure;
using ChatService.Web.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;

namespace ChatService.Web.Utilities;

public class ServiceAvailabilityCheckerUtilities
{
    public static void ThrowIfCosmosUnavailable(CosmosException e)
    {
        if (e.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            throw new ThirdPartyServiceUnavailableException(e.Message);
        }
    }
    
    public static void ThrowIfBlobUnavailable(RequestFailedException e)
    {
        if (e.Status == 503)
        {
            throw new ThirdPartyServiceUnavailableException(e.Message);
        }
    }
}