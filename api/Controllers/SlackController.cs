using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

namespace SlackApp.Controllers;

[ApiController]
[Route("slack")]
[IgnoreAntiforgeryToken]
public class SlackController(IConfiguration configuration) : ControllerBase
{
    [HttpPost("commands")]
    [Consumes("application/x-www-form-urlencoded")]
    public IActionResult Commands([FromForm] SlackCommandPayload cmd)
    {
        var action = cmd.Text.Split(" ")[0];
        
        switch (action)
        {
            case "ping":
                return new JsonResult(new { response_type = "in_channel", text = "pong" });
            case "signin":
                {
                    return new JsonResult(new { response_type = "in_channel", text = $"{GenerateSignInUrl()}" });

                    string GenerateSignInUrl()
                    {
                        var slackUserId = cmd.UserId;
                        var slackTeamId = cmd.TeamId;
                        var channelId = cmd.ChannelId;

                        var secret = configuration["Slack:LinkSecret"];
                        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
                        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                        var token = new JwtSecurityToken(
                            claims:
                            [
                                new Claim("slack_user_id", slackUserId!),
                            new Claim("slack_team_id", slackTeamId!),
                            new Claim("channel_id", channelId!)
                            ],
                            expires: DateTime.UtcNow.AddMinutes(15),
                            signingCredentials: creds
                        );

                        var jwt = new JwtSecurityTokenHandler().WriteToken(token);

                        var selfUrl = Request.Scheme + "://" + Request.Host;
                        var redirectUri = Uri.EscapeDataString($"{selfUrl}/slack/link-callback");
                        var url = $"{configuration["WebOrigin"]}/integrations/slack/link?state={jwt}&redirectUri={redirectUri}";

                        return url;
                    }
                }
            case "subscribe":
                {
                    var project = cmd.Text.Split(" ")[1];
                    return new JsonResult(new { response_type = "in_channel", text = $"🔔 {cmd.UserName} subscribed to *{project}*" });
                }
            default:
                return new JsonResult(new { text = "Unknown command" });
        }
    }
    
    [HttpPost("link-callback")]
    [Authorize]
    public async Task<IActionResult> Link(LinkCallbackPayload payload)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(configuration["Slack:LinkSecret"]);

        var validationParams = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.FromMinutes(5)
        };

        var principal = tokenHandler.ValidateToken(payload.State, validationParams, out var _);
        var slackUserId = principal.FindFirstValue("slack_user_id");
        var slackTeamId = principal.FindFirstValue("slack_team_id");
        var channelId = principal.FindFirstValue("channel_id");

        if (string.IsNullOrEmpty(slackUserId) || string.IsNullOrEmpty(slackTeamId))
            return BadRequest("Invalid state payload");

        await SaveSlackMappingAsync(userId, slackUserId, slackTeamId, channelId);
        
        var slackBotToken = await GetSlackBotTokenAsync(slackTeamId);

        await SendSlackEphemeralAsync(slackBotToken, channelId!,
            "✅ Your account has been linked successfully!");
        
        return Ok(new { message = "Slack account linked successfully" });
    }

    private async Task<string> GetSlackBotTokenAsync(string slackTeamId)
    {
        using var dbConnection = new NpgsqlConnection(configuration.GetConnectionString("DefaultConnection"));
        const string sql =
    """
        SELECT "BotAccessToken" FROM "SlackInstallations" WHERE "TeamId" = @TeamId; 
    """;
        var token = await dbConnection.QuerySingleAsync<string>(sql, new { TeamId = slackTeamId });

        return token;
    }

    private async Task SaveSlackMappingAsync(string? userId, string slackUserId, string slackTeamId, string? channelId)
    {
        var json = "[]";
        var path = "maps.json";
        
        if (System.IO.File.Exists(path))
        {
            json = await System.IO.File.ReadAllTextAsync(path);
        }
        
        var maps = JsonSerializer.Deserialize<List<Map>>(json) ?? [];
        maps.Add(new Map()
        {
            UserId = userId,
            SlackUserId = slackUserId,
            SlackTeamId = slackTeamId,
            ChannelId = channelId
        });
        
        json = JsonSerializer.Serialize(maps);

        await System.IO.File.WriteAllTextAsync(path, json);
    }
    
    private async Task SendSlackEphemeralAsync(string botToken, string channelId, string text)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", botToken);
        
        var joinPayload = new { channel = channelId };
        
        var payload = new
        {
            channel = channelId,
            text
        };

        var response = await http.PostAsJsonAsync("https://slack.com/api/chat.postMessage", payload);
        var json = await response.Content.ReadAsStringAsync();
    }
}

public class Map
{
    public string? UserId { get; set; }
    public string? SlackUserId { get; set; }
    public string? SlackTeamId { get; set; }
    public string? ChannelId { get; set; }
}

public class LinkCallbackPayload
{
    public string State { get; set; }
}