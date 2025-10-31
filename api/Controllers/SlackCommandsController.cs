using Microsoft.AspNetCore.Mvc;
using SlackApp.Services;

namespace SlackApp.Controllers;

[ApiController]
[Route("slack/commands")]
[IgnoreAntiforgeryToken]
public class SlackCommandsController(IConfiguration configuration, SlackAppService slackAppService) : ControllerBase
{
    
    [HttpPost("ping")]
    [Consumes("application/x-www-form-urlencoded")]
    public IActionResult Ping([FromForm] SlackCommandPayload cmd)
    {
        return new JsonResult(new { response_type = "in_channel", text = "pong" });
    }
    
    [HttpPost("signin")]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> SignIn([FromForm] SlackCommandPayload cmd)
    {
        var link = slackAppService.GenerateSignInUrl(cmd.UserId, cmd.TeamId, cmd.ChannelId, HttpContext);
        
        return new JsonResult(new { response_type = "in_channel", text = link });
    }
}