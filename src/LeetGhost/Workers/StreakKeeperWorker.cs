using Cronos;
using LeetGhost.Configuration;
using LeetGhost.Data.Entities;
using LeetGhost.Data.Repositories.Interfaces;
using LeetGhost.Models;
using LeetGhost.Services;
using LeetGhost.Telegram;
using Microsoft.Extensions.Options;

namespace LeetGhost.Workers;

/// <summary>
/// Background worker that monitors and maintains LeetCode streaks for all users.
/// </summary>
public class StreakKeeperWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ScheduleSettings _settings;
    private readonly ILogger<StreakKeeperWorker> _logger;
    private readonly CronExpression _cronExpression;

    public StreakKeeperWorker(
        IServiceProvider serviceProvider,
        IOptions<ScheduleSettings> settings,
        ILogger<StreakKeeperWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _settings = settings.Value;
        _logger = logger;
        _cronExpression = CronExpression.Parse(_settings.CheckCronExpression);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Streak Keeper Worker started");
        _logger.LogInformation("Schedule: {Cron}", _settings.CheckCronExpression);
        _logger.LogInformation("Automation time: {Hour:D2}:{Minute:D2}", _settings.AutomationStartHour, _settings.AutomationStartMinute);

        // Initial check on startup
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        await RunCheckForAllUsersAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTimeOffset.UtcNow;
            var next = _cronExpression.GetNextOccurrence(now, TimeZoneInfo.Utc);

            if (next == null)
            {
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                continue;
            }

            var delay = next.Value - now;
            _logger.LogDebug("Next check in {Delay}", delay);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await RunCheckForAllUsersAsync(stoppingToken);
        }
    }

    private async Task RunCheckForAllUsersAsync(CancellationToken ct)
    {
        _logger.LogInformation("Running streak check for all users");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var solutionRepo = scope.ServiceProvider.GetRequiredService<ISolutionRepository>();
            var submissionRepo = scope.ServiceProvider.GetRequiredService<ISubmissionLogRepository>();
            var leetCodeApi = scope.ServiceProvider.GetRequiredService<LeetCodeApiService>();
            var telegramBot = scope.ServiceProvider.GetRequiredService<TelegramBotService>();

            var users = await userRepo.GetAllActiveUsersAsync(ct);
            _logger.LogInformation("Found {Count} active users", users.Count);

            foreach (var user in users)
            {
                try
                {
                    await ProcessUserAsync(user, userRepo, solutionRepo, submissionRepo, leetCodeApi, telegramBot, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing user {UserId}", user.Id);

                    // Notify user of error
                    await telegramBot.SendNotificationAsync(user.TelegramChatId,
                        $"⚠️ Error checking your streak: {ex.Message}\n\nYour session may have expired. Use /bind to reconnect.", ct);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in streak check");
        }
    }

    private async Task ProcessUserAsync(
        UserEntity user,
        IUserRepository userRepo,
        ISolutionRepository solutionRepo,
        ISubmissionLogRepository submissionRepo,
        LeetCodeApiService leetCodeApi,
        TelegramBotService telegramBot,
        CancellationToken ct)
    {
        var timeZone = GetTimeZone(user.TimeZone);
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
        var currentTime = TimeOnly.FromDateTime(now);
        var automationTime = new TimeOnly(_settings.AutomationStartHour, _settings.AutomationStartMinute);

        // Check if automation threshold reached
        if (currentTime < automationTime)
        {
            _logger.LogDebug("User {UserId}: before automation threshold ({Current} < {Threshold})",
                user.Id, currentTime, automationTime);
            return;
        }

        // Check streak status
        var status = await leetCodeApi.GetStreakStatusAsync(user, ct);
        await userRepo.UpdateLastSuccessfulAuthAsync(user.TelegramChatId, ct);

        if (status.HasSubmittedToday)
        {
            _logger.LogDebug("User {UserId}: already submitted today, streak safe", user.Id);
            return;
        }

        // Check if we already auto-submitted today
        var todaySubmission = await submissionRepo.GetTodaySubmissionAsync(user.Id, timeZone, ct);
        if (todaySubmission is { IsAutomatic: true })
        {
            _logger.LogDebug("User {UserId}: already auto-submitted today", user.Id);
            return;
        }

        _logger.LogInformation("User {UserId}: needs auto-submission", user.Id);

        // Get the next solution to submit (prioritizes unsubmitted, falls back to submitted)
        var solution = await solutionRepo.GetNextForSubmissionAsync(user.Id, ct: ct);
        if (solution == null)
        {
            _logger.LogWarning("User {UserId}: no solutions available", user.Id);
            await telegramBot.SendNotificationAsync(user.TelegramChatId,
                "⚠️ No solutions available for auto-submission!\n\nAdd solutions with /add or /addsubmitted to protect your streak.", ct);
            return;
        }

        var solutionType = solution.IsSubmittedToLeetCode ? "resubmitting" : "submitting prepared";
        // Submit
        _logger.LogInformation("User {UserId}: {Type} {Problem}", user.Id, solutionType, solution.ProblemSlug);
        var result = await leetCodeApi.SubmitSolutionAsync(user, solution, ct);

        // Log the submission
        await submissionRepo.LogAsync(new SubmissionLogEntity
        {
            UserId = user.Id,
            SolutionId = solution.Id,
            ProblemSlug = solution.ProblemSlug,
            SubmittedAt = DateTime.UtcNow,
            LeetCodeSubmissionId = result.SubmissionId,
            Status = result.Status.ToString(),
            RuntimeMs = result.RuntimeMs,
            MemoryMb = result.MemoryMb,
            IsAutomatic = true,
            ErrorMessage = result.ErrorMessage
        }, ct);

        // Notify user
        if (result.Status == SubmissionStatus.Accepted)
        {
            await solutionRepo.MarkAsSubmittedAsync(solution.Id, ct);

            var typeEmoji = solution.IsSubmittedToLeetCode ? "🔄" : "📦";
            var typeText = solution.IsSubmittedToLeetCode ? "Resubmitted" : "Submitted prepared solution";
            
            var message = $"""
                🔥 <b>Streak Saved!</b>

                {typeEmoji} {typeText}: <code>{solution.ProblemSlug}</code>
                ⚡ Runtime: {result.RuntimeMs}ms
                💾 Memory: {result.MemoryMb:F1} MB
                🏆 Current streak: {status.CurrentStreak + 1} days
                """;
            await telegramBot.SendNotificationAsync(user.TelegramChatId, message, ct);

            _logger.LogInformation("User {UserId}: streak saved", user.Id);
        }
        else
        {
            var message = $"""
                ❌ <b>Auto-submission Failed</b>

                Problem: <code>{solution.ProblemSlug}</code>
                Status: {result.Status}
                Error: {result.ErrorMessage}

                Your streak may be at risk! Use /submit to try another solution.
                """;
            await telegramBot.SendNotificationAsync(user.TelegramChatId, message, ct);

            _logger.LogWarning("User {UserId}: submission failed - {Status}", user.Id, result.Status);
        }
    }

    private static TimeZoneInfo GetTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch
        {
            return TimeZoneInfo.Utc;
        }
    }
}
