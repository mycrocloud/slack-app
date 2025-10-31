using SlackApp.Extensions;

namespace SlackApp.Middlewares;

public class ReadSlackRequestBodyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger _logger;
    
    public ReadSlackRequestBodyMiddleware(RequestDelegate next, ILogger<ReadSlackRequestBodyMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        if (context.Request.IsSlackCommandRequest())
        {
            var requestBody = await context.ReadRequestBody();
            
            _logger.LogInformation("Request Body:{@requestBody}", requestBody);
            context.Items.Add("Slack:Body", requestBody);
        }

        await _next(context);
    }
}