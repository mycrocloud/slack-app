using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using SlackApp.Services;

namespace SlackApp.Authentications;

public class SlackAuthenticationHandler: AuthenticationHandler<SlackAuthenticationOptions>
{
    private readonly SlackAppService _slackAppService;

    public SlackAuthenticationHandler(IOptionsMonitor<SlackAuthenticationOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock, SlackAppService slackAppService) : base(options, logger, encoder, clock)
    {
        _slackAppService = slackAppService;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var body = Context.Items["Slack:Body"] as string;

        var query = QueryHelpers.ParseQuery(body);
        
        var teamId = query["team_id"];
        var slackUserId = query["user_id"];

        var userId = await _slackAppService.FindUserId(teamId, slackUserId);

        if (userId is null)
        {
            return AuthenticateResult.Fail("Invalid token");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new("SlackTeamId", teamId),
            new("SlackUserId", slackUserId),
        };
        
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        
        var claimPrincipal = new ClaimsPrincipal(identity);
        
        var ticket = new AuthenticationTicket(claimPrincipal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status200OK;
        
        await Response.WriteAsJsonAsync(new
        {
            response_type = "ephemeral",
            text = "⚠️ You need to sign in to MycroCloud first. Use `/mycrocloud login` to connect your account."
        });
    }
}

public class SlackAuthenticationOptions : AuthenticationSchemeOptions
{
}