using LeetGhost.Data.Entities;
using LeetGhost.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LeetGhost.Data.Repositories;

/// <summary>
/// SQLite implementation of submission log repository.
/// </summary>
public class SubmissionLogRepository(LeetGhostDbContext db) : ISubmissionLogRepository
{
    public async Task<SubmissionLogEntity> LogAsync(SubmissionLogEntity log, CancellationToken ct = default)
    {
        db.SubmissionLogs.Add(log);
        await db.SaveChangesAsync(ct);
        return log;
    }

    public async Task<IReadOnlyList<SubmissionLogEntity>> GetRecentForUserAsync(int userId, int count = 10, CancellationToken ct = default)
    {
        return await db.SubmissionLogs
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.SubmittedAt)
            .Take(count)
            .Include(s => s.Solution)
            .ToListAsync(ct);
    }

    public async Task<bool> HasSubmittedTodayAsync(int userId, TimeZoneInfo timeZone, CancellationToken ct = default)
    {
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
        var todayStart = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Unspecified);
        var todayStartUtc = TimeZoneInfo.ConvertTimeToUtc(todayStart, timeZone);

        return await db.SubmissionLogs
            .AnyAsync(s => s.UserId == userId && 
                          s.SubmittedAt >= todayStartUtc && 
                          s.Status == "Accepted", ct);
    }

    public async Task<SubmissionLogEntity?> GetTodaySubmissionAsync(int userId, TimeZoneInfo timeZone, CancellationToken ct = default)
    {
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
        var todayStart = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Unspecified);
        var todayStartUtc = TimeZoneInfo.ConvertTimeToUtc(todayStart, timeZone);

        return await db.SubmissionLogs
            .Where(s => s.UserId == userId && s.SubmittedAt >= todayStartUtc)
            .OrderByDescending(s => s.SubmittedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<(int total, int successful)> GetStatsForUserAsync(int userId, CancellationToken ct = default)
    {
        var total = await db.SubmissionLogs.CountAsync(s => s.UserId == userId, ct);
        var successful = await db.SubmissionLogs.CountAsync(s => s.UserId == userId && s.Status == "Accepted", ct);
        return (total, successful);
    }
}
