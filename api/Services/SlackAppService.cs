using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Dapper;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using SlackApp.Controllers;

namespace SlackApp.Services;

public class SlackAppService
{
    private readonly IConfiguration _configuration;
    private readonly LinkGenerator _linkGenerator;

    public SlackAppService(IConfiguration configuration, LinkGenerator linkGenerator)
    {
        _configuration = configuration;
        _linkGenerator = linkGenerator;
    }
    
    public string GenerateSignInUrl(string slackUserId, string slackTeamId, string channelId, HttpContext context)
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
        
        var redirectUri = _linkGenerator.GetUriByAction(context, nameof(SlackIntegrationController.Link), SlackIntegrationController.ControllerName)!;
        redirectUri = Uri.EscapeDataString(redirectUri);
        var url = $"{_configuration["WebOrigin"]}/integrations/slack/link?state={jwt}&redirectUri={redirectUri}";

        return url;
    }
    
    private async Task<string> GetSlackBotTokenAsync(string slackTeamId)
    {
        await using var dbConnection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
        const string sql =
            """
                SELECT "BotAccessToken" FROM "SlackInstallations" WHERE "TeamId" = @TeamId; 
            """;
        var token = await dbConnection.QuerySingleAsync<string>(sql, new { TeamId = slackTeamId });

        return token;
    }
    
    public async Task SendSlackEphemeralAsync(string slackTeamId, string channelId, string text)
    {
        var botToken = await GetSlackBotTokenAsync(slackTeamId);
        
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", botToken);
        
        var payload = new
        {
            channel = channelId,
            text
        };

        var response = await http.PostAsJsonAsync("https://slack.com/api/chat.postMessage", payload);
        var json = await response.Content.ReadAsStringAsync();
    }

    public async Task LinkSlackUser(string slackUserId, string slackTeamId, string userId)
    {
        const string sql = 
    """
    insert into "SlackUserLinks" ("TeamId", "SlackUserId", "UserId", "LinkedAt")
    values (@TeamId, @SlackUserId, @UserId, @LinkedAt);
    """;
        
        await using var dbConnection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
        
        await dbConnection.ExecuteAsync(sql, new
        {
            TeamId = slackTeamId,
            SlackUserId = slackUserId,
            UserId = userId,
            LinkedAt = DateTime.UtcNow
        });
    }
}