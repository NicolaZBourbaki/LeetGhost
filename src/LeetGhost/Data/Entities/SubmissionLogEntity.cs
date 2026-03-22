namespace LeetGhost.Data.Entities;

/// <summary>
/// Log of submission attempts.
/// </summary>
public class SubmissionLogEntity
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
    /// Foreign key to solution (nullable if solution was deleted).
    /// </summary>
    public int? SolutionId { get; set; }

    /// <summary>
    /// Navigation property.
    /// </summary>
    public SolutionEntity? Solution { get; set; }

    /// <summary>
    /// Problem slug at time of submission.
    /// </summary>
    public string ProblemSlug { get; set; } = string.Empty;

    /// <summary>
    /// When the submission was made.
    /// </summary>
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// LeetCode submission ID.
    /// </summary>
    public string? LeetCodeSubmissionId { get; set; }

    /// <summary>
    /// Submission status (Accepted, WrongAnswer, etc.).
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Runtime in milliseconds.
    /// </summary>
    public int? RuntimeMs { get; set; }

    /// <summary>
    /// Memory usage in MB.
    /// </summary>
    public double? MemoryMb { get; set; }

    /// <summary>
    /// Whether this was an automatic or manual submission.
    /// </summary>
    public bool IsAutomatic { get; set; } = true;

    /// <summary>
    /// Error message if submission failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
