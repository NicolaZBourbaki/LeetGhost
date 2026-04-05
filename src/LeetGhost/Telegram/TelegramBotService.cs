using System.Text;
using System.Text.RegularExpressions;
using LeetGhost.Configuration;
using LeetGhost.Data.Entities;
using LeetGhost.Data.Repositories.Interfaces;
using LeetGhost.Services;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace LeetGhost.Telegram;

/// <summary>
/// Background service that runs the Telegram bot.
/// </summary>
public class TelegramBotService(
    IServiceProvider services,
    IOptions<TelegramBotSettings> settings,
    ILogger<TelegramBotService> logger)
    : BackgroundService
{
    private readonly TelegramBotSettings _settings = settings.Value;
    private TelegramBotClient? _bot;

    // State for users currently in /bind flow
    private readonly Dictionary<long, BindState> _bindStates = new();
    
    // State for users currently in /add flow
    private readonly Dictionary<long, AddSolutionState> _addStates = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrEmpty(_settings.BotToken))
        {
            logger.LogWarning("Telegram bot token not configured. Bot disabled.");
            return;
        }

        _bot = new TelegramBotClient(_settings.BotToken);

        var me = await _bot.GetMe(stoppingToken);
        logger.LogInformation("Telegram bot started: @{Username}", me.Username);

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = [UpdateType.Message]
        };

        _bot.StartReceiving(
            HandleUpdateAsync,
            HandleErrorAsync,
            receiverOptions,
            stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    /// <summary>
    /// Extracts raw text from a message, restoring || characters that Telegram interprets as spoiler markers.
    /// This is needed because code with || (OR operators) gets mangled by Telegram's spoiler formatting.
    /// </summary>
    private static string ExtractRawTextWithSpoilers(Message message)
    {
        var text = message.Text ?? string.Empty;
        var entities = message.Entities;
        
        if (entities == null || entities.Length == 0)
            return text;
        
        // Find spoiler entities and restore the || markers
        var spoilerEntities = entities
            .Where(e => e.Type == MessageEntityType.Spoiler)
            .OrderByDescending(e => e.Offset) // Process from end to start to keep offsets valid
            .ToList();
        
        if (spoilerEntities.Count == 0)
            return text;
        
        var sb = new StringBuilder(text);
        foreach (var entity in spoilerEntities)
        {
            var endPos = entity.Offset + entity.Length;
            if (endPos <= sb.Length)
                sb.Insert(endPos, "||");
            if (entity.Offset <= sb.Length)
                sb.Insert(entity.Offset, "||");
        }
        
        return sb.ToString();
    }

    private async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
    {
        if (update.Message is not { Text: { } text } message)
            return;

        var chatId = message.Chat.Id;
        var username = message.From?.Username;

        // Check if user is allowed
        if (_settings.AllowedChatIds.Count > 0 && !_settings.AllowedChatIds.Contains(chatId))
        {
            await bot.SendMessage(chatId, "⛔ Access denied.", cancellationToken: ct);
            return;
        }

        logger.LogDebug("Message from {ChatId}: {Text}", chatId, text);

        try
        {
            // Check if user is in bind flow
            if (_bindStates.TryGetValue(chatId, out var bindState))
            {
                await HandleBindInputAsync(bot, chatId, text, ct);
                return;
            }
            
            // Check if user is in add solution flow
            if (_addStates.TryGetValue(chatId, out var addState))
            {
                await HandleAddSolutionInputAsync(bot, chatId, message, addState, ct);
                return;
            }

            // Handle commands
            var command = text.Split(' ')[0].ToLowerInvariant();
            if (command.Contains('@'))
                command = command.Split('@')[0];

            switch (command)
            {
                case "/start":
                case "/help":
                    await HandleHelpAsync(bot, chatId, ct);
                    break;
                case "/bind":
                    await HandleBindStartAsync(bot, chatId, username, ct);
                    break;
                case "/status":
                    await HandleStatusAsync(bot, chatId, ct);
                    break;
                case "/check":
                    await HandleCheckConnectionAsync(bot, chatId, ct);
                    break;
                case "/submit":
                    await HandleSubmitAsync(bot, chatId, ct);
                    break;
                case "/solutions":
                    await HandleSolutionsAsync(bot, chatId, ct);
                    break;
                case "/add":
                    await HandleAddStartAsync(bot, chatId, false, ct);
                    break;
                case "/addsubmitted":
                    await HandleAddStartAsync(bot, chatId, true, ct);
                    break;
                case "/delete":
                    await HandleDeleteAsync(bot, chatId, text, ct);
                    break;
                case "/toggle":
                    await HandleToggleAsync(bot, chatId, text, ct);
                    break;
                case "/stats":
                    await HandleStatsAsync(bot, chatId, ct);
                    break;
                case "/view":
                    await HandleViewSolutionAsync(bot, chatId, text, ct);
                    break;
                case "/pause":
                    await HandlePauseAsync(bot, chatId, true, ct);
                    break;
                case "/resume":
                    await HandlePauseAsync(bot, chatId, false, ct);
                    break;
                default:
                    await bot.SendMessage(chatId, "Unknown command. Use /help for available commands.", cancellationToken: ct);
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling message from {ChatId}", chatId);
            await bot.SendMessage(chatId, $"❌ Error: {ex.Message}", cancellationToken: ct);
        }
    }

    private async Task HandleHelpAsync(ITelegramBotClient bot, long chatId, CancellationToken ct)
    {
        var help = """
            🔥 <b>LeetGhost</b> - LeetCode Streak Keeper

            <b>Commands:</b>
            /bind - Connect your LeetCode account
            /status - Check streak status
            /submit - Submit a solution now
            
            <b>Solution Management:</b>
            /solutions - List your solutions
            /view [id] - View solution code
            /add - Add a prepared (unsubmitted) solution
            /addsubmitted - Add an already submitted solution
            /delete [id] - Delete a solution
            /toggle [id] - Enable/disable a solution
            
            <b>Settings:</b>
            /check - Check LeetCode connection health
            /stats - View submission statistics
            /pause - Pause auto-submissions
            /resume - Resume auto-submissions
            /help - Show this message

            <b>Priority:</b>
            📦 Prepared solutions are submitted first
            🔄 Already submitted solutions are used as fallback

            <b>First time?</b>
            Use /bind to connect your LeetCode account.
            """;
        await bot.SendMessage(chatId, help, parseMode: ParseMode.Html, cancellationToken: ct);
    }

    private async Task HandleBindStartAsync(ITelegramBotClient bot, long chatId, string? username, CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        await userRepo.UpsertAsync(chatId, username, ct);

        _bindStates[chatId] = new BindState();

        var instructions = """
            🔐 <b>Connect LeetCode Account</b>

            1. Go to <a href="https://leetcode.com">leetcode.com</a> and log in
            2. Press <b>F12</b> → <b>Application</b> tab → <b>Cookies</b>
            3. Find <code>leetcode.com</code> cookies

            Send me your credentials in this format:
            <code>SESSION:your_leetcode_session_value
            CSRF:your_csrftoken_value</code>

            ⚠️ Keep "Remember me" checked when logging in for longer sessions.
            
            Type /cancel to abort.
            """;
        await bot.SendMessage(chatId, instructions, parseMode: ParseMode.Html, linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true }, cancellationToken: ct);
    }

    private async Task HandleBindInputAsync(ITelegramBotClient bot, long chatId, string text, CancellationToken ct)
    {
        if (text.ToLowerInvariant() == "/cancel")
        {
            _bindStates.Remove(chatId);
            await bot.SendMessage(chatId, "Cancelled.", cancellationToken: ct);
            return;
        }

        // Parse SESSION: and CSRF:
        var sessionMatch = Regex.Match(text, @"SESSION[:\s]+(\S+)", RegexOptions.IgnoreCase);
        var csrfMatch = Regex.Match(text, @"CSRF[:\s]+(\S+)", RegexOptions.IgnoreCase);

        if (!sessionMatch.Success || !csrfMatch.Success)
        {
            await bot.SendMessage(chatId, "❌ Invalid format. Please send:\n<code>SESSION:value\nCSRF:value</code>", parseMode: ParseMode.Html, cancellationToken: ct);
            return;
        }

        var sessionCookie = sessionMatch.Groups[1].Value;
        var csrfToken = csrfMatch.Groups[1].Value;

        await bot.SendMessage(chatId, "🔄 Validating credentials...", cancellationToken: ct);

        using var scope = services.CreateScope();
        var leetCodeApi = scope.ServiceProvider.GetRequiredService<LeetCodeApiService>();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();

        var username = await leetCodeApi.ValidateCredentialsAsync(sessionCookie, csrfToken, ct);

        if (username == null)
        {
            await bot.SendMessage(chatId, "❌ Invalid or expired credentials. Please try again.", cancellationToken: ct);
            return;
        }

        await userRepo.UpdateCredentialsAsync(chatId, sessionCookie, csrfToken, username, ct);
        await userRepo.UpdateLastSuccessfulAuthAsync(chatId, ct);

        _bindStates.Remove(chatId);

        await bot.SendMessage(chatId, 
            $"✅ Connected as <b>{username}</b>!\n\nUse /status to check your streak.", 
            parseMode: ParseMode.Html, 
            cancellationToken: ct);
    }

    private async Task HandleStatusAsync(ITelegramBotClient bot, long chatId, CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var solutionRepo = scope.ServiceProvider.GetRequiredService<ISolutionRepository>();
        var leetCodeApi = scope.ServiceProvider.GetRequiredService<LeetCodeApiService>();

        var user = await userRepo.GetByTelegramChatIdAsync(chatId, ct);
        if (user == null || !user.HasValidCredentials)
        {
            await bot.SendMessage(chatId, "⚠️ Not connected. Use /bind first.", cancellationToken: ct);
            return;
        }

        await bot.SendMessage(chatId, "🔄 Checking streak...", cancellationToken: ct);

        var status = await leetCodeApi.GetStreakStatusAsync(user, ct);
        await userRepo.UpdateLastSuccessfulAuthAsync(chatId, ct);
        
        var (total, unsubmitted, submitted) = await solutionRepo.GetCountsForUserAsync(user.Id, ct);

        var emoji = status.HasSubmittedToday ? "✅" : "⚠️";
        var streakEmoji = status.CurrentStreak > 0 ? "🔥" : "";

        var message = $"""
            {emoji} <b>Streak Status</b>

            {streakEmoji} Current streak: <b>{status.CurrentStreak}</b> days
            🏆 Longest streak: <b>{status.LongestStreak}</b> days
            📅 Submitted today: {(status.HasSubmittedToday ? "Yes" : "No")}
            👤 User: {user.LeetCodeUsername}
            ⚙️ Auto-submit: {(user.IsEnabled ? "Enabled" : "Paused")}
            
            <b>Solutions:</b>
            📦 Prepared: {unsubmitted}
            🔄 Submitted: {submitted}
            📊 Total: {total}
            """;

        await bot.SendMessage(chatId, message, parseMode: ParseMode.Html, cancellationToken: ct);
    }

    private async Task HandleCheckConnectionAsync(ITelegramBotClient bot, long chatId, CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var leetCodeApi = scope.ServiceProvider.GetRequiredService<LeetCodeApiService>();

        var user = await userRepo.GetByTelegramChatIdAsync(chatId, ct);
        if (user == null || !user.HasValidCredentials)
        {
            await bot.SendMessage(chatId, "⚠️ Not connected. Use /bind first.", cancellationToken: ct);
            return;
        }

        await bot.SendMessage(chatId, "🔄 Checking LeetCode connection...", cancellationToken: ct);

        var validatedUsername = await leetCodeApi.ValidateCredentialsAsync(user.SessionCookie!, user.CsrfToken!, ct);

        if (validatedUsername != null)
        {
            await userRepo.UpdateLastSuccessfulAuthAsync(chatId, ct);
            
            // Calculate credential age
            var credentialAge = DateTime.UtcNow - (user.CredentialsUpdatedAt ?? user.CreatedAt);
            var lastSuccess = user.LastSuccessfulAuthAt.HasValue 
                ? DateTime.UtcNow - user.LastSuccessfulAuthAt.Value 
                : (TimeSpan?)null;

            // Estimate session health based on age
            string healthEmoji, healthText;
            if (credentialAge.TotalDays < 3)
            {
                healthEmoji = "🟢";
                healthText = "Excellent - session is fresh";
            }
            else if (credentialAge.TotalDays < 7)
            {
                healthEmoji = "🟡";
                healthText = "Good - consider refreshing in a few days";
            }
            else if (credentialAge.TotalDays < 12)
            {
                healthEmoji = "🟠";
                healthText = "Aging - refresh soon recommended";
            }
            else
            {
                healthEmoji = "🔴";
                healthText = "Old - may expire anytime, refresh now";
            }

            var message = $"""
                ✅ <b>Connection Active</b>

                👤 User: <b>{validatedUsername}</b>
                {healthEmoji} Health: {healthText}
                
                📅 Credentials set: {FormatTimeAgo(credentialAge)}
                🔑 Last verified: {(lastSuccess.HasValue ? FormatTimeAgo(lastSuccess.Value) : "just now")}
                
                💡 <i>LeetCode sessions typically last 1-2 weeks with "Remember me" enabled.</i>
                """;
            await bot.SendMessage(chatId, message, parseMode: ParseMode.Html, cancellationToken: ct);
        }
        else
        {
            var message = """
                ❌ <b>Connection Failed</b>

                Your LeetCode session has expired or is invalid.

                Use /bind to reconnect with fresh credentials.
                """;
            await bot.SendMessage(chatId, message, parseMode: ParseMode.Html, cancellationToken: ct);
        }
    }

    private static string FormatTimeAgo(TimeSpan timeSpan)
    {
        if (timeSpan.TotalMinutes < 1) return "just now";
        if (timeSpan.TotalMinutes < 60) return $"{(int)timeSpan.TotalMinutes} minutes ago";
        if (timeSpan.TotalHours < 24) return $"{(int)timeSpan.TotalHours} hours ago";
        if (timeSpan.TotalDays < 2) return "1 day ago";
        return $"{(int)timeSpan.TotalDays} days ago";
    }

    private async Task HandleSubmitAsync(ITelegramBotClient bot, long chatId, CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var solutionRepo = scope.ServiceProvider.GetRequiredService<ISolutionRepository>();
        var submissionRepo = scope.ServiceProvider.GetRequiredService<ISubmissionLogRepository>();
        var leetCodeApi = scope.ServiceProvider.GetRequiredService<LeetCodeApiService>();

        var user = await userRepo.GetByTelegramChatIdAsync(chatId, ct);
        if (user == null || !user.HasValidCredentials)
        {
            await bot.SendMessage(chatId, "⚠️ Not connected. Use /bind first.", cancellationToken: ct);
            return;
        }

        // Use prioritized selection (unsubmitted first)
        var solution = await solutionRepo.GetNextForSubmissionAsync(user.Id, ct: ct);
        if (solution == null)
        {
            await bot.SendMessage(chatId, "❌ No solutions available. Add some with /add or /addsubmitted!", cancellationToken: ct);
            return;
        }

        var typeText = solution.IsSubmittedToLeetCode ? "resubmitting" : "submitting prepared";
        await bot.SendMessage(chatId, $"🚀 {typeText.Substring(0, 1).ToUpper() + typeText.Substring(1)} <code>{solution.ProblemSlug}</code> ({solution.Language})...", parseMode: ParseMode.Html, cancellationToken: ct);

        var result = await leetCodeApi.SubmitSolutionAsync(user, solution, ct);

        // Log submission
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
            IsAutomatic = false,
            ErrorMessage = result.ErrorMessage
        }, ct);

        if (result.Status == Models.SubmissionStatus.Accepted)
        {
            var wasPrepared = !solution.IsSubmittedToLeetCode;
            await solutionRepo.MarkAsSubmittedAsync(solution.Id, ct);
            await userRepo.UpdateLastSuccessfulAuthAsync(chatId, ct);

            var typeNote = wasPrepared ? "\n📦 <i>Prepared solution is now marked as submitted</i>" : "";
            var msg = $"""
                ✅ <b>Accepted!</b>

                📝 Problem: <code>{solution.ProblemSlug}</code>
                💻 Language: {solution.Language}
                ⚡ Runtime: {result.RuntimeMs}ms
                💾 Memory: {result.MemoryMb:F1} MB{typeNote}
                """;
            await bot.SendMessage(chatId, msg, parseMode: ParseMode.Html, cancellationToken: ct);
        }
        else
        {
            var msg = $"❌ <b>{result.Status}</b>\n\n{result.ErrorMessage}";
            await bot.SendMessage(chatId, msg, parseMode: ParseMode.Html, cancellationToken: ct);
        }
    }

    private async Task HandleSolutionsAsync(ITelegramBotClient bot, long chatId, CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var solutionRepo = scope.ServiceProvider.GetRequiredService<ISolutionRepository>();

        var user = await userRepo.GetByTelegramChatIdAsync(chatId, ct);
        if (user == null)
        {
            await bot.SendMessage(chatId, "⚠️ Not connected. Use /bind first.", cancellationToken: ct);
            return;
        }

        var solutions = await solutionRepo.GetAllForUserAsync(user.Id, ct);
        if (solutions.Count == 0)
        {
            await bot.SendMessage(chatId, "📭 No solutions yet.\n\nUse /add to add prepared solutions or /addsubmitted for already submitted ones.", cancellationToken: ct);
            return;
        }
        
        var (total, unsubmitted, submitted) = await solutionRepo.GetCountsForUserAsync(user.Id, ct);

        var sb = new StringBuilder("📋 <b>Your Solutions</b>\n\n");
        
        // Group by state
        var unsubmittedSolutions = solutions.Where(s => !s.IsSubmittedToLeetCode).ToList();
        var submittedSolutions = solutions.Where(s => s.IsSubmittedToLeetCode).ToList();
        
        if (unsubmittedSolutions.Any())
        {
            sb.AppendLine("📦 <b>Prepared (will be used first):</b>");
            foreach (var s in unsubmittedSolutions.Take(10))
            {
                var status = s.IsEnabled ? "✅" : "⏸️";
                sb.AppendLine($"  {status} <code>{s.Id}</code>: {s.ProblemSlug} ({s.Language})");
            }
            if (unsubmittedSolutions.Count > 10)
                sb.AppendLine($"  ...and {unsubmittedSolutions.Count - 10} more");
            sb.AppendLine();
        }
        
        if (submittedSolutions.Any())
        {
            sb.AppendLine("🔄 <b>Already Submitted (fallback):</b>");
            foreach (var s in submittedSolutions.Take(10))
            {
                var status = s.IsEnabled ? "✅" : "⏸️";
                sb.AppendLine($"  {status} <code>{s.Id}</code>: {s.ProblemSlug} ({s.Language}) [{s.SubmissionCount}x]");
            }
            if (submittedSolutions.Count > 10)
                sb.AppendLine($"  ...and {submittedSolutions.Count - 10} more");
            sb.AppendLine();
        }

        sb.AppendLine($"Total: {total} | Prepared: {unsubmitted} | Submitted: {submitted}");
        sb.AppendLine("\n<code>/toggle [id]</code> - enable/disable");
        sb.AppendLine("<code>/delete [id]</code> - delete solution");

        await bot.SendMessage(chatId, sb.ToString(), parseMode: ParseMode.Html, cancellationToken: ct);
    }

    private async Task HandleAddStartAsync(ITelegramBotClient bot, long chatId, bool isAlreadySubmitted, CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();

        var user = await userRepo.GetByTelegramChatIdAsync(chatId, ct);
        if (user == null)
        {
            await bot.SendMessage(chatId, "⚠️ Not connected. Use /bind first.", cancellationToken: ct);
            return;
        }

        _addStates[chatId] = new AddSolutionState { IsAlreadySubmitted = isAlreadySubmitted };

        var typeText = isAlreadySubmitted ? "already submitted" : "prepared (unsubmitted)";
        var instructions = $"""
            📝 <b>Add {typeText} solution</b>

            <b>Step 1/3:</b> Send the problem slug
            
            Example: <code>two-sum</code>
            
            💡 The slug is the URL part after /problems/
            https://leetcode.com/problems/<b>two-sum</b>/
            
            Type /cancel to abort.
            """;
        await bot.SendMessage(chatId, instructions, parseMode: ParseMode.Html, cancellationToken: ct);
    }

    private async Task HandleAddSolutionInputAsync(ITelegramBotClient bot, long chatId, Message message, AddSolutionState state, CancellationToken ct)
    {
        var text = message.Text ?? string.Empty;
        
        if (text.ToLowerInvariant() == "/cancel")
        {
            _addStates.Remove(chatId);
            await bot.SendMessage(chatId, "Cancelled.", cancellationToken: ct);
            return;
        }

        // Step 1: Problem slug
        if (state.ProblemSlug == null)
        {
            var slug = text.Trim().ToLowerInvariant();
            // Clean up if user pasted full URL
            if (slug.Contains("leetcode.com/problems/"))
            {
                var match = Regex.Match(slug, @"problems/([^/]+)");
                if (match.Success)
                    slug = match.Groups[1].Value;
            }
            
            if (string.IsNullOrWhiteSpace(slug) || slug.Length < 2)
            {
                await bot.SendMessage(chatId, "❌ Invalid slug. Please try again.", cancellationToken: ct);
                return;
            }

            state.ProblemSlug = slug;
            
            var langMsg = """
                <b>Step 2/3:</b> Send the language
                
                Supported: <code>python3</code>, <code>java</code>, <code>cpp</code>, <code>c#</code>, <code>javascript</code>, <code>typescript</code>, <code>go</code>, <code>rust</code>, <code>kotlin</code>, <code>swift</code>
                """;
            await bot.SendMessage(chatId, langMsg, parseMode: ParseMode.Html, cancellationToken: ct);
            return;
        }

        // Step 2: Language
        if (state.Language == null)
        {
            var lang = text.Trim().ToLowerInvariant();
            var validLangs = new[] { "python3", "python", "java", "cpp", "c++", "csharp", "c#", "javascript", "js", "typescript", "ts", "go", "golang", "rust", "kotlin", "swift", "ruby", "scala", "php" };
            
            // Normalize common aliases
            lang = lang switch
            {
                "python" => "python3",
                "c++" => "cpp",
                "c#" => "csharp",
                "js" => "javascript",
                "ts" => "typescript",
                "golang" => "go",
                _ => lang
            };

            if (!validLangs.Contains(lang))
            {
                await bot.SendMessage(chatId, "❌ Unsupported language. Please use one from the list.", cancellationToken: ct);
                return;
            }

            state.Language = lang;
            
            var codeMsg = """
                <b>Step 3/3:</b> Send the solution code
                
                💡 Paste your complete solution class/function.
                """;
            await bot.SendMessage(chatId, codeMsg, parseMode: ParseMode.Html, cancellationToken: ct);
            return;
        }

        // Step 3: Code - Use raw text extraction to restore || operators that Telegram treats as spoiler markers
        if (state.Code == null)
        {
            var code = ExtractRawTextWithSpoilers(message).Trim();
            if (code.Length < 10)
            {
                await bot.SendMessage(chatId, "❌ Code seems too short. Please paste the complete solution.", cancellationToken: ct);
                return;
            }

            state.Code = code;

            // Save the solution
            using var scope = services.CreateScope();
            var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var solutionRepo = scope.ServiceProvider.GetRequiredService<ISolutionRepository>();

            var user = await userRepo.GetByTelegramChatIdAsync(chatId, ct);
            if (user == null)
            {
                _addStates.Remove(chatId);
                await bot.SendMessage(chatId, "❌ User not found.", cancellationToken: ct);
                return;
            }

            var solution = new SolutionEntity
            {
                UserId = user.Id,
                ProblemSlug = state.ProblemSlug,
                ProblemTitle = state.ProblemSlug.Replace("-", " "),
                Language = state.Language,
                Code = state.Code,
                IsSubmittedToLeetCode = state.IsAlreadySubmitted,
                IsEnabled = true,
                AddedAt = DateTime.UtcNow
            };

            await solutionRepo.AddAsync(solution, ct);
            _addStates.Remove(chatId);

            var typeEmoji = state.IsAlreadySubmitted ? "🔄" : "📦";
            var typeText = state.IsAlreadySubmitted ? "Already Submitted" : "Prepared";
            
            var successMsg = $"""
                ✅ <b>Solution Added!</b>

                {typeEmoji} Type: {typeText}
                📝 Problem: <code>{state.ProblemSlug}</code>
                💻 Language: {state.Language}
                🆔 ID: {solution.Id}
                
                {(state.IsAlreadySubmitted 
                    ? "This solution will be used as fallback when no prepared solutions are available." 
                    : "This solution will be prioritized for the next auto-submission.")}
                """;
            await bot.SendMessage(chatId, successMsg, parseMode: ParseMode.Html, cancellationToken: ct);
        }
    }

    private async Task HandleDeleteAsync(ITelegramBotClient bot, long chatId, string text, CancellationToken ct)
    {
        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || !int.TryParse(parts[1], out var solutionId))
        {
            await bot.SendMessage(chatId, "Usage: <code>/delete [solution_id]</code>\n\nUse /solutions to see IDs.", parseMode: ParseMode.Html, cancellationToken: ct);
            return;
        }

        using var scope = services.CreateScope();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var solutionRepo = scope.ServiceProvider.GetRequiredService<ISolutionRepository>();

        var user = await userRepo.GetByTelegramChatIdAsync(chatId, ct);
        if (user == null)
        {
            await bot.SendMessage(chatId, "⚠️ Not connected.", cancellationToken: ct);
            return;
        }

        var solution = await solutionRepo.GetByIdAsync(solutionId, ct);
        if (solution == null || solution.UserId != user.Id)
        {
            await bot.SendMessage(chatId, "❌ Solution not found.", cancellationToken: ct);
            return;
        }

        await solutionRepo.DeleteAsync(solutionId, ct);

        await bot.SendMessage(chatId, 
            $"🗑️ Deleted <code>{solution.ProblemSlug}</code> ({solution.Language}).", 
            parseMode: ParseMode.Html, 
            cancellationToken: ct);
    }

    private async Task HandleToggleAsync(ITelegramBotClient bot, long chatId, string text, CancellationToken ct)
    {
        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || !int.TryParse(parts[1], out var solutionId))
        {
            await bot.SendMessage(chatId, "Usage: <code>/toggle [solution_id]</code>\n\nUse /solutions to see IDs.", parseMode: ParseMode.Html, cancellationToken: ct);
            return;
        }

        using var scope = services.CreateScope();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var solutionRepo = scope.ServiceProvider.GetRequiredService<ISolutionRepository>();

        var user = await userRepo.GetByTelegramChatIdAsync(chatId, ct);
        if (user == null)
        {
            await bot.SendMessage(chatId, "⚠️ Not connected.", cancellationToken: ct);
            return;
        }

        var solution = await solutionRepo.GetByIdAsync(solutionId, ct);
        if (solution == null || solution.UserId != user.Id)
        {
            await bot.SendMessage(chatId, "❌ Solution not found.", cancellationToken: ct);
            return;
        }

        await solutionRepo.ToggleEnabledAsync(solutionId, ct);
        var newState = !solution.IsEnabled;

        await bot.SendMessage(chatId, 
            $"{(newState ? "✅" : "⏸️")} <code>{solution.ProblemSlug}</code> is now {(newState ? "enabled" : "disabled")}.", 
            parseMode: ParseMode.Html, 
            cancellationToken: ct);
    }

    private async Task HandleViewSolutionAsync(ITelegramBotClient bot, long chatId, string text, CancellationToken ct)
    {
        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || !int.TryParse(parts[1], out var solutionId))
        {
            await bot.SendMessage(chatId, "Usage: <code>/view [solution_id]</code>\n\nUse /solutions to see IDs.", parseMode: ParseMode.Html, cancellationToken: ct);
            return;
        }

        using var scope = services.CreateScope();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var solutionRepo = scope.ServiceProvider.GetRequiredService<ISolutionRepository>();

        var user = await userRepo.GetByTelegramChatIdAsync(chatId, ct);
        if (user == null)
        {
            await bot.SendMessage(chatId, "⚠️ Not connected.", cancellationToken: ct);
            return;
        }

        var solution = await solutionRepo.GetByIdAsync(solutionId, ct);
        if (solution == null || solution.UserId != user.Id)
        {
            await bot.SendMessage(chatId, "❌ Solution not found.", cancellationToken: ct);
            return;
        }

        var typeEmoji = solution.IsSubmittedToLeetCode ? "🔄" : "📦";
        var typeText = solution.IsSubmittedToLeetCode ? "Already Submitted" : "Prepared";
        var statusText = solution.IsEnabled ? "Enabled" : "Disabled";
        
        // Escape HTML entities in code for display
        var escapedCode = solution.Code
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");

        var message = $"""
            {typeEmoji} <b>Solution #{solution.Id}</b>

            📝 Problem: <code>{solution.ProblemSlug}</code>
            💻 Language: {solution.Language}
            📊 Type: {typeText}
            ⚙️ Status: {statusText}
            📅 Added: {solution.AddedAt:yyyy-MM-dd HH:mm} UTC
            🔄 Submissions: {solution.SubmissionCount}

            <b>Code:</b>
            <pre>{escapedCode}</pre>
            """;

        // Telegram has a 4096 character limit per message
        if (message.Length > 4000)
        {
            // Send info first, then code separately
            var infoMessage = $"""
                {typeEmoji} <b>Solution #{solution.Id}</b>

                📝 Problem: <code>{solution.ProblemSlug}</code>
                💻 Language: {solution.Language}
                📊 Type: {typeText}
                ⚙️ Status: {statusText}
                📅 Added: {solution.AddedAt:yyyy-MM-dd HH:mm} UTC
                🔄 Submissions: {solution.SubmissionCount}
                """;
            await bot.SendMessage(chatId, infoMessage, parseMode: ParseMode.Html, cancellationToken: ct);
            
            // Send code in chunks if needed
            var codeMessage = $"<b>Code:</b>\n<pre>{escapedCode}</pre>";
            if (codeMessage.Length > 4000)
            {
                // Split code into chunks
                var chunkSize = 3900;
                for (var i = 0; i < escapedCode.Length; i += chunkSize)
                {
                    var chunk = escapedCode.Substring(i, Math.Min(chunkSize, escapedCode.Length - i));
                    var prefix = i == 0 ? "<b>Code:</b>\n" : "";
                    await bot.SendMessage(chatId, $"{prefix}<pre>{chunk}</pre>", parseMode: ParseMode.Html, cancellationToken: ct);
                }
            }
            else
            {
                await bot.SendMessage(chatId, codeMessage, parseMode: ParseMode.Html, cancellationToken: ct);
            }
        }
        else
        {
            await bot.SendMessage(chatId, message, parseMode: ParseMode.Html, cancellationToken: ct);
        }
    }

    private async Task HandleStatsAsync(ITelegramBotClient bot, long chatId, CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var solutionRepo = scope.ServiceProvider.GetRequiredService<ISolutionRepository>();
        var submissionRepo = scope.ServiceProvider.GetRequiredService<ISubmissionLogRepository>();

        var user = await userRepo.GetByTelegramChatIdAsync(chatId, ct);
        if (user == null)
        {
            await bot.SendMessage(chatId, "⚠️ Not connected. Use /bind first.", cancellationToken: ct);
            return;
        }

        var (total, successful) = await submissionRepo.GetStatsForUserAsync(user.Id, ct);
        var solutionCount = await solutionRepo.GetCountForUserAsync(user.Id, ct);
        var recentLogs = await submissionRepo.GetRecentForUserAsync(user.Id, 5, ct);

        var sb = new StringBuilder("📊 <b>Statistics</b>\n\n");
        sb.AppendLine($"📝 Solutions: {solutionCount}");
        sb.AppendLine($"🚀 Total submissions: {total}");
        sb.AppendLine($"✅ Successful: {successful}");
        sb.AppendLine($"📈 Success rate: {(total > 0 ? (successful * 100.0 / total):0):F0}%");

        if (recentLogs.Count > 0)
        {
            sb.AppendLine("\n<b>Recent:</b>");
            foreach (var log in recentLogs)
            {
                var emoji = log.Status == "Accepted" ? "✅" : "❌";
                sb.AppendLine($"{emoji} {log.ProblemSlug} - {log.SubmittedAt:MMM dd HH:mm}");
            }
        }

        await bot.SendMessage(chatId, sb.ToString(), parseMode: ParseMode.Html, cancellationToken: ct);
    }

    private async Task HandlePauseAsync(ITelegramBotClient bot, long chatId, bool pause, CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();

        var user = await userRepo.GetByTelegramChatIdAsync(chatId, ct);
        if (user == null)
        {
            await bot.SendMessage(chatId, "⚠️ Not connected.", cancellationToken: ct);
            return;
        }

        await userRepo.SetEnabledAsync(chatId, !pause, ct);

        var message = pause
            ? "⏸️ Auto-submissions paused. Use /resume to continue."
            : "▶️ Auto-submissions resumed!";

        await bot.SendMessage(chatId, message, cancellationToken: ct);
    }

    private Task HandleErrorAsync(ITelegramBotClient bot, Exception exception, CancellationToken ct)
    {
        logger.LogError(exception, "Telegram bot error");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Sends a notification to a user.
    /// </summary>
    public async Task SendNotificationAsync(long chatId, string message, CancellationToken ct = default)
    {
        if (_bot == null) return;
        await _bot.SendMessage(chatId, message, parseMode: ParseMode.Html, cancellationToken: ct);
    }

    private class BindState
    {
        public DateTime StartedAt { get; } = DateTime.UtcNow;
    }

    private class AddSolutionState
    {
        public DateTime StartedAt { get; } = DateTime.UtcNow;
        public bool IsAlreadySubmitted { get; set; }
        public string? ProblemSlug { get; set; }
        public string? Language { get; set; }
        public string? Code { get; set; }
    }
}
