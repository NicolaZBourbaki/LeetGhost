namespace LeetGhost.Models;

/// <summary>
/// Represents the result of a LeetCode submission.
/// </summary>
public class SubmissionResult
{
    /// <summary>
    /// Submission ID from LeetCode.
    /// </summary>
    public string SubmissionId { get; set; } = string.Empty;

    /// <summary>
    /// Status of the submission.
    /// </summary>
    public SubmissionStatus Status { get; set; }

    /// <summary>
    /// Runtime in milliseconds (if accepted).
    /// </summary>
    public int? RuntimeMs { get; set; }

    /// <summary>
    /// Memory usage in MB (if accepted).
    /// </summary>
    public double? MemoryMb { get; set; }

    /// <summary>
    /// Runtime percentile (if accepted).
    /// </summary>
    public double? RuntimePercentile { get; set; }

    /// <summary>
    /// Memory percentile (if accepted).
    /// </summary>
    public double? MemoryPercentile { get; set; }

    /// <summary>
    /// Error message if submission failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// When the submission was made.
    /// </summary>
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The problem slug that was submitted.
    /// </summary>
    public string ProblemSlug { get; set; } = string.Empty;

    /// <summary>
    /// The language used for submission.
    /// </summary>
    public string Language { get; set; } = string.Empty;

    public bool IsSuccess => Status == SubmissionStatus.Accepted;
}

public enum SubmissionStatus
{
    Pending,
    Accepted,
    WrongAnswer,
    TimeLimitExceeded,
    MemoryLimitExceeded,
    RuntimeError,
    CompileError,
    Unknown
}
