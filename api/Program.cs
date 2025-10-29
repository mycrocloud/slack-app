using Microsoft.AspNetCore.Mvc;
using SlackApp;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpLogging(_ => { });

var app = builder.Build();

app.UseHttpLogging();

app.UseMiddleware<SlackSignatureVerificationMiddleware>();

app.MapPost("/slack/commands", async ([FromForm] SlackCommandPayload command) =>
    {
        return Results.Ok(new
        {
            response_type = "ephemeral",
            text = $"I received your command: {command.Text}",
        });
    })
    .DisableAntiforgery();

app.Run();

public class SlackCommandPayload
{
    [FromForm(Name = "token")] public string? Token { get; set; }
    [FromForm(Name = "text")] public string? Text { get; set; }
}
