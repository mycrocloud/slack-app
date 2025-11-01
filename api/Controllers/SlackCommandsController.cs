using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SlackApp.Authentication;
using SlackApp.Extensions;
using SlackApp.Services;

namespace SlackApp.Controllers;

[ApiController]
[Route("slack/commands")]
[Authorize(AuthenticationSchemes = SlackDefaults.AuthenticationScheme)]
[IgnoreAntiforgeryToken]
public class SlackCommandsController(SlackAppService slackAppService) : ControllerBase
{
    [HttpPost("ping")]
    [AllowAnonymous]
    [Consumes("application/x-www-form-urlencoded")]
    public IActionResult Ping()
    {
        return new JsonResult(new { response_type = "ephemeral", text = "pong" });
    }
    
    [HttpPost("signin")]
    [AllowAnonymous]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> SignIn([FromForm] SlackCommandPayload cmd)
    {
        if (User.Identity.IsAuthenticated)
        {
            var text = $"""
                        You are already signed in to MycroCloud with {User.GetUserId()}.
                        To sign out, run `/mycrocloud login`
                        """;
            
            return new JsonResult(new { response_type = "in_channel", text });
        }
        
        var link = slackAppService.GenerateSignInUrl(cmd.UserId!, cmd.TeamId!, cmd.ChannelId, HttpContext);

        return new JsonResult(new
        {
            response_type = "ephemeral", blocks = new object[]
            {
                new
                {
                    type = "section",
                    text = new { type = "mrkdwn", text = "Finish connecting your Vercel account" }
                },
                new
                {
                    type = "actions",
                    elements = new object[]
                    {
                        new
                        {
                            type = "button",
                            text = new { type = "plain_text", text = "Continue with MycroCloud", emoji = true },
                            url = link,
                            style = "primary"
                        }
                    }
                }
            }
        });
    }
    
    [HttpPost("signout")]
    [Consumes("application/x-www-form-urlencoded")]
    public new async Task<IActionResult> SignOut()
    {
        await slackAppService.LogOut(User.GetSlackTeamId(), User.GetSlackUserId());
        
        return new JsonResult(new { response_type = "ephemeral", text = "Your account was successfully unlinked." });
    }
    
    [HttpPost("whoami")]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> WhoAmI()
    {
        var userId = await slackAppService.GetUserId(User.GetSlackTeamId(), User.GetSlackUserId());
        
        return new JsonResult(new { response_type = "ephemeral", text = userId });
    }
}