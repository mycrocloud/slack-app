using Microsoft.AspNetCore.Mvc;
using SlackApp;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpLogging(_ => { });

var app = builder.Build();

app.UseHttpLogging();

app.UseMiddleware<SlackSignatureVerificationMiddleware>();

app.MapPost("/slack/commands", async ([FromForm] SlackCommandPayload command) =>
    {
        var (action, project) = ParseCommand(command.Text);
        
        if (action == "subscribe")
            return Results.Json(new { response_type = "in_channel", text = $"🔔 {command.UserName} subscribed to *{project}*" });
        
        return Results.Json(new { text = "Unknown command" });
    })
    .DisableAntiforgery();

app.Run();

(string action, string project) ParseCommand(string text)
{
    var parts = text.Split(' ', 2);
    return (parts[0], parts.Length > 1 ? parts[1] : "");
}

public class SlackCommandPayload
{
    [FromForm(Name = "token")] public string? Token { get; set; }
    [FromForm(Name = "team_id")] public string? TeamId { get; set; }
    [FromForm(Name = "team_domain")] public string? TeamDomain { get; set; }
    [FromForm(Name = "enterprise_id")] public string? EnterpriseId { get; set; }
    [FromForm(Name = "enterprise_name")] public string? EnterpriseName { get; set; }
    [FromForm(Name = "channel_id")] public string? ChannelId { get; set; }
    [FromForm(Name = "channel_name")] public string? ChannelName { get; set; }
    [FromForm(Name = "user_id")] public string? UserId { get; set; }
    [FromForm(Name = "user_name")] public string? UserName { get; set; }
    [FromForm(Name = "command")] public string? Command { get; set; }
    [FromForm(Name = "text")] public string? Text { get; set; }
    [FromForm(Name = "response_url")] public string? ResponseUrl { get; set; }
    [FromForm(Name = "trigger_id")] public string? TriggerId { get; set; }
    [FromForm(Name = "api_app_id")] public string? ApiAppId { get; set; }
}
