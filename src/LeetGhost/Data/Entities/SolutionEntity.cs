namespace LeetGhost.Data.Entities;

/// <summary>
/// Represents a stored solution for a LeetCode problem.
/// </summary>
public class SolutionEntity
{
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to user.
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// Navigation property.
    /// </summary>
    public UserEntity User { get; set; } = null!;

    /// <summary>
    /// LeetCode problem slug (e.g., "two-sum").
    /// </summary>
    public string ProblemSlug { get; set; } = string.Empty;

    /// <summary>
    /// Problem title for display purposes.
    /// </summary>
    public string ProblemTitle { get; set; } = string.Empty;

    /// <summary>
    /// Programming language of the solution.
    /// </summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// The actual solution code.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// When this solution was added.
    /// </summary>
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this solution was last submitted.
    /// </summary>
    public DateTime? LastSubmittedAt { get; set; }

    /// <summary>
    /// Number of times this solution has been auto-submitted.
    /// </summary>
    public int SubmissionCount { get; set; }

    /// <summary>
    /// Whether this solution is enabled for auto-submission.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Whether this solution has been submitted to LeetCode at least once.
    /// Unsubmitted (prepared) solutions are prioritized over already submitted ones.
    /// </summary>
    public bool IsSubmittedToLeetCode { get; set; } = false;

    /// <summary>
    /// Optional notes about the solution.
    /// </summary>
    public string? Notes { get; set; }
}
