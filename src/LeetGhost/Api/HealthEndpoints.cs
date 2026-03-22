using LeetGhost.Configuration;
using LeetGhost.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LeetGhost.Api;

/// <summary>
/// API endpoints for service health and configuration.
/// </summary>
public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api")
            .WithTags("Health");

        group.MapGet("/health", GetHealth)
            .WithName("GetHealth")
            .WithSummary("Health check")
            .WithDescription("Returns the health status of the service");

        group.MapGet("/config", GetConfig)
            .WithName("GetConfig")
            .WithSummary("Get current configuration")
            .WithDescription("Returns the current schedule configuration");
    }

    private static async Task<IResult> GetHealth(LeetGhostDbContext db)
    {
        var canConnect = await db.Database.CanConnectAsync();
        var userCount = canConnect ? await db.Users.CountAsync() : 0;

        return Results.Ok(new
        {
            status = canConnect ? "healthy" : "unhealthy",
            timestamp = DateTime.UtcNow,
            service = "LeetGhost",
            version = "1.0.0",
            database = canConnect ? "connected" : "disconnected",
            users = userCount
        });
    }

    private static IResult GetConfig(
        IOptions<ScheduleSettings> scheduleSettings,
        IOptions<TelegramBotSettings> telegramSettings)
    {
        var schedule = scheduleSettings.Value;
        var telegram = telegramSettings.Value;

        return Results.Ok(new
        {
            schedule = new
            {
                schedule.CheckCronExpression,
                schedule.AutomationStartHour,
                schedule.AutomationStartMinute,
                schedule.TimeZone
            },
            telegram = new
            {
                configured = !string.IsNullOrEmpty(telegram.BotToken),
                restrictedAccess = telegram.AllowedChatIds.Count > 0
            }
        });
    }
}
