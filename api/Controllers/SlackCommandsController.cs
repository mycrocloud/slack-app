using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SlackApp.Extensions;
using SlackApp.Services;

namespace SlackApp.Controllers;

[ApiController]
[Route("slack/commands")]
[Authorize]
[IgnoreAntiforgeryToken]
public class SlackCommandsController(SlackAppService slackAppService) : ControllerBase
{
    [HttpPost("ping")]
    [AllowAnonymous]
    [Consumes("application/x-www-form-urlencoded")]
    public IActionResult Ping()
    {
        return new JsonResult(new { response_type = "in_channel", text = "pong" });
    }
    
    [HttpPost("login")]
    [AllowAnonymous]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> SignIn([FromForm] SlackCommandPayload cmd)
    {
        var link = slackAppService.GenerateSignInUrl(cmd.UserId!, cmd.TeamId!, cmd.ChannelId, HttpContext);
        
        return new JsonResult(new { response_type = "ephemeral", text = link });
    }
    
    [HttpPost("logout")]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> LogOut()
    {
        await slackAppService.LogOut(User.GetSlackTeamId(), User.GetSlackUserId());
        
        return new JsonResult(new { response_type = "ephemeral", text = "Bye!" });
    }
    
    [HttpPost("whoami")]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> WhoAmI()
    {
        var userId = await slackAppService.GetUserId(User.GetSlackTeamId(), User.GetSlackUserId());
        
        return new JsonResult(new { response_type = "ephemeral", text = userId });
    }
}