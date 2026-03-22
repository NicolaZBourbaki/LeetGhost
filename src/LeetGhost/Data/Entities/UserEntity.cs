namespace LeetGhost.Data.Entities;

/// <summary>
/// Represents a user bound via Telegram.
/// </summary>
public class UserEntity
{
    public int Id { get; set; }

    /// <summary>
    /// Telegram chat ID for this user.
    /// </summary>
    public long TelegramChatId { get; set; }

    /// <summary>
    /// Telegram username (optional).
    /// </summary>
    public string? TelegramUsername { get; set; }

    /// <summary>
    /// LeetCode username.
    /// </summary>
    public string? LeetCodeUsername { get; set; }

    /// <summary>
    /// LeetCode session cookie.
    /// </summary>
    public string? SessionCookie { get; set; }

    /// <summary>
    /// LeetCode CSRF token.
    /// </summary>
    public string? CsrfToken { get; set; }

    /// <summary>
    /// When credentials were last updated.
    /// </summary>
    public DateTime? CredentialsUpdatedAt { get; set; }

    /// <summary>
    /// Last successful API call timestamp.
    /// </summary>
    public DateTime? LastSuccessfulAuthAt { get; set; }

    /// <summary>
    /// Whether auto-submission is enabled for this user.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// User's preferred timezone (IANA format).
    /// </summary>
    public string TimeZone { get; set; } = "UTC";

    /// <summary>
    /// When this user was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation property for solutions.
    /// </summary>
    public ICollection<SolutionEntity> Solutions { get; set; } = new List<SolutionEntity>();

    /// <summary>
    /// Navigation property for submission logs.
    /// </summary>
    public ICollection<SubmissionLogEntity> SubmissionLogs { get; set; } = new List<SubmissionLogEntity>();

    /// <summary>
    /// Check if user has valid LeetCode credentials.
    /// </summary>
    public bool HasValidCredentials => 
        !string.IsNullOrEmpty(SessionCookie) && 
        !string.IsNullOrEmpty(CsrfToken);
}
