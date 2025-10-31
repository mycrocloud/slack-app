using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using SlackApp.Services;

namespace SlackApp.Controllers;

[ApiController]
[Route("slack/integration")]
[IgnoreAntiforgeryToken]
public class SlackIntegrationController(IConfiguration configuration, SlackAppService slackAppService) : ControllerBase
{
    public const string ControllerName = "SlackIntegration";
    
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
        
        await slackAppService.SendSlackEphemeralAsync(slackTeamId, channelId!, $"✅ You are signed in to MycroCloud as {userId}");
        
        return Ok(new { message = "Slack account linked successfully" });
    }

    private async Task SaveSlackMappingAsync(string? userId, string slackUserId, string slackTeamId, string? channelId)
    {
        //TODO: implement
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