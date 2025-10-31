using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace SlackApp.Services;

public class SlackAppService
{
    private readonly IConfiguration _configuration;

    public SlackAppService(IConfiguration configuration)
    {
        _configuration = configuration;
    }
    
    public string GenerateSignInUrl(string slackUserId, string slackTeamId, string channelId, string hostUrl)
    {
        var secret = _configuration["Slack:LinkSecret"];
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

        var redirectUri = Uri.EscapeDataString($"{hostUrl}/slack/link-callback");
        var url = $"{_configuration["WebOrigin"]}/integrations/slack/link?state={jwt}&redirectUri={redirectUri}";

        return url;
    }
}