using LeetGhost.Data.Entities;

namespace LeetGhost.Data.Repositories.Interfaces;

/// <summary>
/// Repository for submission logs.
/// </summary>
public interface ISubmissionLogRepository
{
    /// <summary>
    /// Logs a submission.
    /// </summary>
    Task<SubmissionLogEntity> LogAsync(SubmissionLogEntity log, CancellationToken ct = default);

    /// <summary>
    /// Gets recent submissions for a user.
    /// </summary>
    Task<IReadOnlyList<SubmissionLogEntity>> GetRecentForUserAsync(int userId, int count = 10, CancellationToken ct = default);

    /// <summary>
    /// Checks if user has submitted today (in their timezone).
    /// </summary>
    Task<bool> HasSubmittedTodayAsync(int userId, TimeZoneInfo timeZone, CancellationToken ct = default);

    /// <summary>
    /// Gets today's submission for a user.
    /// </summary>
    Task<SubmissionLogEntity?> GetTodaySubmissionAsync(int userId, TimeZoneInfo timeZone, CancellationToken ct = default);

    /// <summary>
    /// Gets submission stats for a user.
    /// </summary>
    Task<(int total, int successful)> GetStatsForUserAsync(int userId, CancellationToken ct = default);
}
