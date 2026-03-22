using LeetGhost.Data.Entities;
using LeetGhost.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LeetGhost.Data.Repositories;

/// <summary>
/// SQLite implementation of solution repository.
/// </summary>
public class SolutionRepository(LeetGhostDbContext db) : ISolutionRepository
{
    private readonly Random _random = new();

    public async Task<IReadOnlyList<SolutionEntity>> GetAllForUserAsync(int userId, CancellationToken ct = default)
    {
        return await db.Solutions
            .Where(s => s.UserId == userId)
            .OrderBy(s => s.IsSubmittedToLeetCode) // Unsubmitted first
            .ThenBy(s => s.ProblemSlug)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SolutionEntity>> GetEnabledForUserAsync(int userId, CancellationToken ct = default)
    {
        return await db.Solutions
            .Where(s => s.UserId == userId && s.IsEnabled)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SolutionEntity>> GetUnsubmittedForUserAsync(int userId, CancellationToken ct = default)
    {
        return await db.Solutions
            .Where(s => s.UserId == userId && s.IsEnabled && !s.IsSubmittedToLeetCode)
            .OrderBy(s => s.AddedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SolutionEntity>> GetSubmittedForUserAsync(int userId, CancellationToken ct = default)
    {
        return await db.Solutions
            .Where(s => s.UserId == userId && s.IsEnabled && s.IsSubmittedToLeetCode)
            .OrderBy(s => s.SubmissionCount)
            .ThenBy(s => s.LastSubmittedAt ?? DateTime.MinValue)
            .ToListAsync(ct);
    }

    public async Task<SolutionEntity?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await db.Solutions.FindAsync([id], ct);
    }

    public async Task<SolutionEntity?> GetNextForSubmissionAsync(int userId, IEnumerable<int>? excludeIds = null, CancellationToken ct = default)
    {
        var excludeSet = excludeIds?.ToHashSet() ?? new HashSet<int>();

        // First, try to get an unsubmitted solution (prioritized)
        var unsubmitted = await db.Solutions
            .Where(s => s.UserId == userId && s.IsEnabled && !s.IsSubmittedToLeetCode && !excludeSet.Contains(s.Id))
            .OrderBy(s => s.AddedAt) // Oldest first (FIFO)
            .FirstOrDefaultAsync(ct);

        if (unsubmitted != null)
            return unsubmitted;

        // Fall back to already submitted solutions
        // Select randomly from solutions with the minimum submission count
        var submitted = await db.Solutions
            .Where(s => s.UserId == userId && s.IsEnabled && s.IsSubmittedToLeetCode && !excludeSet.Contains(s.Id))
            .ToListAsync(ct);

        if (submitted.Count == 0)
            return null;

        // Find the minimum submission count
        var minCount = submitted.Min(s => s.SubmissionCount);
        
        // Get all solutions with the minimum count
        var leastSubmitted = submitted.Where(s => s.SubmissionCount == minCount).ToList();
        
        // Pick randomly from the least submitted solutions
        return leastSubmitted[_random.Next(leastSubmitted.Count)];
    }

    public async Task<SolutionEntity?> GetRandomForUserAsync(int userId, IEnumerable<int>? excludeIds = null, CancellationToken ct = default)
    {
        // Delegate to GetNextForSubmissionAsync for prioritized selection
        return await GetNextForSubmissionAsync(userId, excludeIds, ct);
    }

    public async Task<SolutionEntity> AddAsync(SolutionEntity solution, CancellationToken ct = default)
    {
        db.Solutions.Add(solution);
        await db.SaveChangesAsync(ct);
        return solution;
    }

    public async Task UpdateAsync(SolutionEntity solution, CancellationToken ct = default)
    {
        db.Solutions.Update(solution);
        await db.SaveChangesAsync(ct);
    }

    public async Task MarkAsSubmittedAsync(int id, CancellationToken ct = default)
    {
        var solution = await db.Solutions.FindAsync([id], ct);
        if (solution != null)
        {
            solution.LastSubmittedAt = DateTime.UtcNow;
            solution.SubmissionCount++;
            solution.IsSubmittedToLeetCode = true;
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var solution = await db.Solutions.FindAsync([id], ct);
        if (solution != null)
        {
            db.Solutions.Remove(solution);
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task ToggleEnabledAsync(int id, CancellationToken ct = default)
    {
        var solution = await db.Solutions.FindAsync([id], ct);
        if (solution != null)
        {
            solution.IsEnabled = !solution.IsEnabled;
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task<int> GetCountForUserAsync(int userId, CancellationToken ct = default)
    {
        return await db.Solutions.CountAsync(s => s.UserId == userId, ct);
    }

    public async Task<(int Total, int Unsubmitted, int Submitted)> GetCountsForUserAsync(int userId, CancellationToken ct = default)
    {
        var solutions = await db.Solutions
            .Where(s => s.UserId == userId && s.IsEnabled)
            .Select(s => s.IsSubmittedToLeetCode)
            .ToListAsync(ct);

        var total = solutions.Count;
        var submitted = solutions.Count(s => s);
        var unsubmitted = total - submitted;

        return (total, unsubmitted, submitted);
    }
}
