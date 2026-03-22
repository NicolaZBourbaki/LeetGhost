namespace LeetGhost.Models;

/// <summary>
/// Represents the current streak status for the user.
/// </summary>
public class StreakStatus
{
    /// <summary>
    /// Whether the user has made a submission today.
    /// </summary>
    public bool HasSubmittedToday { get; set; }

    /// <summary>
    /// Current streak count.
    /// </summary>
    public int CurrentStreak { get; set; }

    /// <summary>
    /// Longest streak ever achieved.
    /// </summary>
    public int LongestStreak { get; set; }

    /// <summary>
    /// Timestamp of the last submission.
    /// </summary>
    public DateTime? LastSubmissionTime { get; set; }

    /// <summary>
    /// The date being checked (in user's timezone).
    /// </summary>
    public DateOnly CheckDate { get; set; }

    /// <summary>
    /// Whether automation is needed to save the streak.
    /// </summary>
    public bool NeedsAutomation { get; set; }

    /// <summary>
    /// Additional status message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// When this status was retrieved.
    /// </summary>
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
}
