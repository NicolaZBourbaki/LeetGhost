using LeetGhost.Data.Entities;

namespace LeetGhost.Data.Repositories.Interfaces;

/// <summary>
/// Repository for solution management.
/// </summary>
public interface ISolutionRepository
{
    /// <summary>
    /// Gets all solutions for a user.
    /// </summary>
    Task<IReadOnlyList<SolutionEntity>> GetAllForUserAsync(int userId, CancellationToken ct = default);

    /// <summary>
    /// Gets enabled solutions for a user.
    /// </summary>
    Task<IReadOnlyList<SolutionEntity>> GetEnabledForUserAsync(int userId, CancellationToken ct = default);

    /// <summary>
    /// Gets unsubmitted (prepared) solutions for a user.
    /// </summary>
    Task<IReadOnlyList<SolutionEntity>> GetUnsubmittedForUserAsync(int userId, CancellationToken ct = default);

    /// <summary>
    /// Gets already submitted solutions for a user.
    /// </summary>
    Task<IReadOnlyList<SolutionEntity>> GetSubmittedForUserAsync(int userId, CancellationToken ct = default);

    /// <summary>
    /// Gets a solution by ID.
    /// </summary>
    Task<SolutionEntity?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Gets the next solution to submit for a user.
    /// Prioritizes unsubmitted solutions first, then falls back to submitted ones.
    /// </summary>
    Task<SolutionEntity?> GetNextForSubmissionAsync(int userId, IEnumerable<int>? excludeIds = null, CancellationToken ct = default);

    /// <summary>
    /// Gets a random enabled solution for a user.
    /// </summary>
    Task<SolutionEntity?> GetRandomForUserAsync(int userId, IEnumerable<int>? excludeIds = null, CancellationToken ct = default);

    /// <summary>
    /// Adds a new solution.
    /// </summary>
    Task<SolutionEntity> AddAsync(SolutionEntity solution, CancellationToken ct = default);

    /// <summary>
    /// Updates a solution.
    /// </summary>
    Task UpdateAsync(SolutionEntity solution, CancellationToken ct = default);

    /// <summary>
    /// Marks a solution as submitted (updates LastSubmittedAt, SubmissionCount, and IsSubmittedToLeetCode).
    /// </summary>
    Task MarkAsSubmittedAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Deletes a solution.
    /// </summary>
    Task DeleteAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Toggles solution enabled state.
    /// </summary>
    Task ToggleEnabledAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Gets solution count for a user.
    /// </summary>
    Task<int> GetCountForUserAsync(int userId, CancellationToken ct = default);

    /// <summary>
    /// Gets solution counts for a user (total, unsubmitted, submitted).
    /// </summary>
    Task<(int Total, int Unsubmitted, int Submitted)> GetCountsForUserAsync(int userId, CancellationToken ct = default);
}
