using ChatService.Web.Exceptions;
using Newtonsoft.Json;

namespace ChatService.Web.Middleware;

public class ExceptionMiddleware
{
    // ExceptionMiddleware code adopted from Professor Nehme Bilal:
    // https://medium.com/technology-earnin/thorough-testing-of-asp-net-core-or-any-web-api-a87bd0585f9b
    private readonly RequestDelegate _next;
    private readonly IWebHostEnvironment _webHostEnvironment;

    public ExceptionMiddleware(RequestDelegate next, IWebHostEnvironment webHostEnvironment)
    {
        _next = next;
        _webHostEnvironment = webHostEnvironment;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception e)
        {
            if (context.Response.HasStarted)
            {
                throw;
            }

            int statusCode = 500;
            if (e is CosmosServiceUnavailableException or BlobServiceUnavailableException)
            {
                statusCode = 503;
            }

            context.Response.Clear();
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";

            var response = new
            {
                Message = e.Message,
                Exception = SerializeException(e)
            };

            var body = JsonConvert.SerializeObject(response);
            await context.Response.WriteAsync(body);
        }
    }

    private string? SerializeException(Exception e)
    {
        if (_webHostEnvironment.IsProduction())
        {
            return null;
        }
        return e.ToString();
    }
}