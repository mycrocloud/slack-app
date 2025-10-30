using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http.Features;

public class SlackVerificationMiddleware
{
    private readonly RequestDelegate _next;

    public SlackVerificationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context, IConfiguration configuration, IHostEnvironment environment)
    {
        if (environment.IsDevelopment())
        {
            await _next(context);
            return;
        }

        if (context.Request.Path.StartsWithSegments("/slack/commands"))
        {
            if (!context.Request.HasFormContentType ||
                !context.Request.ContentType.StartsWith("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("Invalid content type. Expected application/x-www-form-urlencoded");
                return;
            }

            var slackSigningSecret = configuration["Slack:SigningSecret"];
            if (string.IsNullOrEmpty(slackSigningSecret))
            {
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync("Missing Slack signing secret");
                return;
            }

            var signature = context.Request.Headers["X-Slack-Signature"].FirstOrDefault();
            var timestamp = context.Request.Headers["X-Slack-Request-Timestamp"].FirstOrDefault();

            if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(timestamp))
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Missing Slack signature headers");
                return;
            }

            if (!long.TryParse(timestamp, out var ts))
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("Invalid timestamp");
                return;
            }

            var reqTime = DateTimeOffset.FromUnixTimeSeconds(ts);
            if (Math.Abs((DateTimeOffset.UtcNow - reqTime).TotalMinutes) > 5)
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Timestamp too old");
                return;
            }

            context.Request.EnableBuffering();
            string body;
            using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true))
            {
                body = await reader.ReadToEndAsync();
                context.Request.Body.Position = 0;
            }

            var baseString = $"v0:{timestamp}:{body}";
            var keyBytes = Encoding.UTF8.GetBytes(slackSigningSecret);
            var msgBytes = Encoding.UTF8.GetBytes(baseString);

            using var hmac = new HMACSHA256(keyBytes);
            var hashBytes = hmac.ComputeHash(msgBytes);
            var hash = "v0=" + BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

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