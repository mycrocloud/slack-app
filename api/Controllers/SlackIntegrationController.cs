using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
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
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        
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

        await slackAppService.LinkSlackUser(slackUserId, slackTeamId, userId);
        
        await slackAppService.SendSlackEphemeralAsync(slackTeamId, channelId!, $"✅ You are signed in to MycroCloud as {userId}");
        
        return Ok(new { message = "Slack account linked successfully" });
    }
}

public class LinkCallbackPayload
{
    public string State { get; set; }
}