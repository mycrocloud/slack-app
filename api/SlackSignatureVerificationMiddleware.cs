using System.Security.Cryptography;
using System.Text;

namespace SlackApp;

public class SlackSignatureVerificationMiddleware
{
    private readonly RequestDelegate _next;

    public SlackSignatureVerificationMiddleware(RequestDelegate next)
    {
        _next = next;
    }
    
    public async Task Invoke(HttpContext context, IConfiguration configuration)
    {
        var slackSigningSecret = configuration["Slack:SigningSecret"];
        
        if (context.Request.Path.StartsWithSegments("/slack"))
        {
            var signature = context.Request.Headers["X-Slack-Signature"].FirstOrDefault();
            var timestamp = context.Request.Headers["X-Slack-Request-Timestamp"].FirstOrDefault();

            if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(timestamp))
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Missing Slack signature headers");
                return;
            }

            var reqTime = DateTimeOffset.FromUnixTimeSeconds(long.Parse(timestamp));
            if (Math.Abs((DateTimeOffset.UtcNow - reqTime).TotalMinutes) > 5)
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Timestamp too old");
                return;
            }

            context.Request.EnableBuffering();
            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;

            var baseString = $"v0:{timestamp}:{body}";
            var keyBytes = Encoding.UTF8.GetBytes(slackSigningSecret ?? "");
            var msgBytes = Encoding.UTF8.GetBytes(baseString);

            using var hmac = new HMACSHA256(keyBytes);
            var hashBytes = hmac.ComputeHash(msgBytes);
            var hash = "v0=" + BitConverter.ToString(hashBytes).Replace("-", "").ToLower();

            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(hash),
                    Encoding.UTF8.GetBytes(signature)))
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Invalid Slack signature");
                return;
            }
        }

        await _next(context);
    }
}