using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using LeetGhost.Data.Entities;
using LeetGhost.Models;

namespace LeetGhost.Services;

/// <summary>
/// Service for making LeetCode API calls with user-specific credentials.
/// </summary>
public class LeetCodeApiService(IHttpClientFactory httpClientFactory, ILogger<LeetCodeApiService> logger)
{
    private const string BaseUrl = "https://leetcode.com";
    private const string GraphQLEndpoint = "/graphql";
    private const int SubmissionTimeoutSeconds = 30;
    private const int SubmissionCheckDelayMs = 2000;

    private static readonly Dictionary<string, string> LanguageSlugMap = new()
    {
        ["c#"] = "csharp",
        ["csharp"] = "csharp",
        ["c++"] = "cpp",
        ["cpp"] = "cpp",
        ["python"] = "python3",
        ["python3"] = "python3",
        ["java"] = "java",
        ["javascript"] = "javascript",
        ["js"] = "javascript",
        ["typescript"] = "typescript",
        ["ts"] = "typescript",
        ["go"] = "golang",
        ["golang"] = "golang",
        ["rust"] = "rust",
        ["kotlin"] = "kotlin",
        ["swift"] = "swift",
        ["ruby"] = "ruby",
        ["scala"] = "scala",
        ["php"] = "php",
        ["c"] = "c"
    };

    private HttpClient CreateClientForUser(UserEntity user)
    {
        var client = httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(BaseUrl);
        client.DefaultRequestHeaders.Add("Cookie", $"INGRESSCOOKIE={user.SessionCookie}; csrftoken={user.CsrfToken}");
        client.DefaultRequestHeaders.Add("x-csrftoken", user.CsrfToken);
        client.DefaultRequestHeaders.Add("Referer", BaseUrl);
        client.DefaultRequestHeaders.Add("Origin", BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(30);
        return client;
    }

    /// <summary>
    /// Validates credentials by trying to fetch user profile.
    /// Returns username if successful, null if failed.
    /// </summary>
    public async Task<string?> ValidateCredentialsAsync(string sessionCookie, string csrfToken, CancellationToken ct = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(BaseUrl);
            client.DefaultRequestHeaders.Add("Cookie", $"INGRESSCOOKIE={sessionCookie}; csrftoken={csrfToken}");
            client.DefaultRequestHeaders.Add("x-csrftoken", csrfToken);
            client.DefaultRequestHeaders.Add("Referer", BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(15);

            var query = new
            {
                query = @"
                    query globalData {
                        userStatus {
                            username
                            isSignedIn
                        }
                    }"
            };

            var content = new StringContent(JsonSerializer.Serialize(query), Encoding.UTF8, "application/json");
            var response = await client.PostAsync(GraphQLEndpoint, content, ct);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync(ct);
            var json = JsonNode.Parse(body);

            var isSignedIn = json?["data"]?["userStatus"]?["isSignedIn"]?.GetValue<bool>() ?? false;
            if (!isSignedIn)
                return null;

            return json?["data"]?["userStatus"]?["username"]?.GetValue<string>();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Credential validation failed");
            return null;
        }
    }

    /// <summary>
    /// Gets streak status for a user.
    /// </summary>
    public async Task<StreakStatus> GetStreakStatusAsync(UserEntity user, CancellationToken ct = default)
    {
        var timeZone = GetTimeZone(user.TimeZone);
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
        var today = DateOnly.FromDateTime(now);

        try
        {
            using var client = CreateClientForUser(user);

            // Query without year parameter to get all-time stats (longest streak)
            var allTimeQuery = new
            {
                query = @"
                    query userProfileCalendar($username: String!) {
                        matchedUser(username: $username) {
                            userCalendar {
                                streak
                                totalActiveDays
                            }
                        }
                    }",
                variables = new { username = user.LeetCodeUsername }
            };

            var allTimeContent = new StringContent(JsonSerializer.Serialize(allTimeQuery), Encoding.UTF8, "application/json");
            var allTimeResponse = await client.PostAsync(GraphQLEndpoint, allTimeContent, ct);
            allTimeResponse.EnsureSuccessStatusCode();

            var allTimeBody = await allTimeResponse.Content.ReadAsStringAsync(ct);
            var allTimeJson = JsonNode.Parse(allTimeBody);
            var longestStreak = allTimeJson?["data"]?["matchedUser"]?["userCalendar"]?["streak"]?.GetValue<int>() ?? 0;

            // Query with current year to get submission calendar for today check
            var yearQuery = new
            {
                query = @"
                    query userProfileCalendar($username: String!, $year: Int) {
                        matchedUser(username: $username) {
                            userCalendar(year: $year) {
                                submissionCalendar
                            }
                        }
                    }",
                variables = new { username = user.LeetCodeUsername, year = now.Year }
            };

            var yearContent = new StringContent(JsonSerializer.Serialize(yearQuery), Encoding.UTF8, "application/json");
            var yearResponse = await client.PostAsync(GraphQLEndpoint, yearContent, ct);
            yearResponse.EnsureSuccessStatusCode();

            var yearBody = await yearResponse.Content.ReadAsStringAsync(ct);
            var yearJson = JsonNode.Parse(yearBody);
            var submissionCalendar = yearJson?["data"]?["matchedUser"]?["userCalendar"]?["submissionCalendar"]?.GetValue<string>();

            var hasSubmittedToday = false;
            var currentStreak = 0;
            
            if (!string.IsNullOrEmpty(submissionCalendar))
            {
                var calendar = JsonNode.Parse(submissionCalendar);
                if (calendar is JsonObject calendarObj)
                {
                    // Build a set of dates with submissions
                    var submissionDates = new HashSet<DateOnly>();
                    foreach (var entry in calendarObj)
                    {
                        if (long.TryParse(entry.Key, out var timestamp))
                        {
                            var entryDate = DateTimeOffset.FromUnixTimeSeconds(timestamp);
                            var entryLocalDate = TimeZoneInfo.ConvertTime(entryDate, timeZone);
                            submissionDates.Add(DateOnly.FromDateTime(entryLocalDate.DateTime));
                        }
                    }
                    
                    hasSubmittedToday = submissionDates.Contains(today);
                    
                    // Calculate current streak by counting backwards from today (or yesterday if no submission today)
                    var checkDate = hasSubmittedToday ? today : today.AddDays(-1);
                    while (submissionDates.Contains(checkDate))
                    {
                        currentStreak++;
                        checkDate = checkDate.AddDays(-1);
                    }
                }
            }

            return new StreakStatus
            {
                HasSubmittedToday = hasSubmittedToday,
                CurrentStreak = currentStreak,
                LongestStreak = longestStreak,
                CheckDate = today,
                NeedsAutomation = !hasSubmittedToday,
                Message = hasSubmittedToday
                    ? $"Streak safe! {currentStreak} days"
                    : $"No submission today. Current streak: {currentStreak}",
                CheckedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get streak status for user {UserId}", user.Id);
            return new StreakStatus
            {
                HasSubmittedToday = false,
                CheckDate = today,
                NeedsAutomation = false,
                Message = $"Error: {ex.Message}",
                CheckedAt = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Submits a solution for a user.
    /// </summary>
    public async Task<SubmissionResult> SubmitSolutionAsync(UserEntity user, SolutionEntity solution, CancellationToken ct = default)
    {
        var result = new SubmissionResult
        {
            ProblemSlug = solution.ProblemSlug,
            Language = solution.Language
        };

        try
        {
            using var client = CreateClientForUser(user);

            // Get question ID
            var questionId = await GetQuestionIdAsync(client, solution.ProblemSlug, ct);
            if (questionId == null)
            {
                result.Status = SubmissionStatus.Unknown;
                result.ErrorMessage = $"Question not found: {solution.ProblemSlug}";
                return result;
            }

            var langSlug = MapLanguage(solution.Language);

            logger.LogInformation("Submitting {Problem} for user {UserId}", solution.ProblemSlug, user.Id);

            var submitPayload = new
            {
                lang = langSlug,
                question_id = questionId,
                typed_code = solution.Code
            };

            var submitContent = new StringContent(JsonSerializer.Serialize(submitPayload), Encoding.UTF8, "application/json");
            var submitUrl = $"/problems/{solution.ProblemSlug}/submit/";
            var submitResponse = await client.PostAsync(submitUrl, submitContent, ct);
            submitResponse.EnsureSuccessStatusCode();

            var submitBody = await submitResponse.Content.ReadAsStringAsync(ct);
            var submitJson = JsonNode.Parse(submitBody);
            var submissionId = submitJson?["submission_id"]?.GetValue<long>();

            if (submissionId == null)
            {
                result.Status = SubmissionStatus.Unknown;
                result.ErrorMessage = "No submission ID returned";
                return result;
            }

            result.SubmissionId = submissionId.ToString()!;
            logger.LogInformation("Submission ID: {SubmissionId}", result.SubmissionId);

            // Poll for result
            result = await PollSubmissionResultAsync(client, result.SubmissionId, solution, ct);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to submit solution for user {UserId}", user.Id);
            result.Status = SubmissionStatus.Unknown;
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    private async Task<int?> GetQuestionIdAsync(HttpClient client, string problemSlug, CancellationToken ct)
    {
        var query = new
        {
            query = @"
                query questionData($titleSlug: String!) {
                    question(titleSlug: $titleSlug) {
                        questionId
                    }
                }",
            variables = new { titleSlug = problemSlug }
        };

        var content = new StringContent(JsonSerializer.Serialize(query), Encoding.UTF8, "application/json");
        var response = await client.PostAsync(GraphQLEndpoint, content, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);
        var json = JsonNode.Parse(body);
        var questionId = json?["data"]?["question"]?["questionId"]?.GetValue<string>();
        return int.TryParse(questionId, out var id) ? id : null;
    }

    private async Task<SubmissionResult> PollSubmissionResultAsync(HttpClient client, string submissionId, SolutionEntity solution, CancellationToken ct)
    {
        var result = new SubmissionResult
        {
            SubmissionId = submissionId,
            ProblemSlug = solution.ProblemSlug,
            Language = solution.Language,
            Status = SubmissionStatus.Pending
        };

        var checkUrl = $"/submissions/detail/{submissionId}/check/";
        var startTime = DateTime.UtcNow;

        while (DateTime.UtcNow - startTime < TimeSpan.FromSeconds(SubmissionTimeoutSeconds))
        {
            await Task.Delay(SubmissionCheckDelayMs, ct);

            var response = await client.GetAsync(checkUrl, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            var json = JsonNode.Parse(body);

            var state = json?["state"]?.GetValue<string>();
            if (state == "PENDING" || state == "STARTED")
                continue;

            var statusMsg = json?["status_msg"]?.GetValue<string>() ?? "";
            result.Status = ParseSubmissionStatus(statusMsg);

            if (result.Status == SubmissionStatus.Accepted)
            {
                result.RuntimeMs = ParseRuntime(json?["status_runtime"]?.GetValue<string>());
                result.MemoryMb = ParseMemory(json?["status_memory"]?.GetValue<string>());
                logger.LogInformation("Accepted! Runtime: {Runtime}ms", result.RuntimeMs);
            }
            else
            {
                result.ErrorMessage = statusMsg;
                logger.LogWarning("Not accepted: {Status}", statusMsg);
            }

            return result;
        }

        result.Status = SubmissionStatus.Unknown;
        result.ErrorMessage = "Timeout waiting for result";
        return result;
    }

    private static int? ParseRuntime(string? value) =>
        value != null && int.TryParse(value.Replace(" ms", ""), out var ms) ? ms : null;

    private static double? ParseMemory(string? value) =>
        value != null && double.TryParse(value.Replace(" MB", ""), out var mb) ? mb : null;

    private static SubmissionStatus ParseSubmissionStatus(string statusMessage) => statusMessage.ToLowerInvariant() switch
    {
        "accepted" => SubmissionStatus.Accepted,
        "wrong answer" => SubmissionStatus.WrongAnswer,
        "time limit exceeded" => SubmissionStatus.TimeLimitExceeded,
        "memory limit exceeded" => SubmissionStatus.MemoryLimitExceeded,
        "runtime error" => SubmissionStatus.RuntimeError,
        "compile error" => SubmissionStatus.CompileError,
        _ => SubmissionStatus.Unknown
    };

    private static string MapLanguage(string language)
    {
        var key = language.ToLowerInvariant().Trim();
        return LanguageSlugMap.TryGetValue(key, out var mapped) ? mapped : key;
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
