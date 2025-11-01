using Microsoft.AspNetCore.HttpOverrides;
using SlackApp.Authentications;
using SlackApp.Middlewares;
using SlackApp.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services
    .AddAuthentication()
    .AddScheme<SlackAuthenticationOptions, SlackAuthenticationHandler>("Slack", _ => {})
    .AddJwtBearer("MycroCloudApi", options =>
    {
        options.Authority = builder.Configuration["Authentication:Schemes:Auth0JwtBearer:Authority"];
        options.Audience = builder.Configuration["Authentication:Schemes:Auth0JwtBearer:Audience"];
    });

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(builder.Configuration["WebOrigin"])
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddScoped<SlackAppService>();
builder.Services.AddKeyedSingleton<SlackAppService>("SlackAppService");
builder.Services.AddHostedService<SubscribeService>();

var app = builder.Build();
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor
});

app.UseMiddleware<ReadSlackRequestBodyMiddleware>();
app.UseSlackVerification();
app.UseSlackCommandRewrite();

app.UseRouting();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/ping", () => "pong");
app.MapControllers();

app.Run();