using LeetGhost.Data.Entities;
using LeetGhost.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LeetGhost.Data.Repositories;

/// <summary>
/// SQLite implementation of user repository.
/// </summary>
public class UserRepository(LeetGhostDbContext db) : IUserRepository
{
    public async Task<UserEntity?> GetByTelegramChatIdAsync(long chatId, CancellationToken ct = default)
    {
        return await db.Users
            .Include(u => u.Solutions.Where(s => s.IsEnabled))
            .FirstOrDefaultAsync(u => u.TelegramChatId == chatId, ct);
    }

    public async Task<IReadOnlyList<UserEntity>> GetAllActiveUsersAsync(CancellationToken ct = default)
    {
        return await db.Users
            .Where(u => u.IsEnabled && u.SessionCookie != null && u.CsrfToken != null)
            .Include(u => u.Solutions.Where(s => s.IsEnabled))
            .ToListAsync(ct);
    }

    public async Task<UserEntity> UpsertAsync(long chatId, string? username = null, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.TelegramChatId == chatId, ct);
        
        if (user == null)
        {
            user = new UserEntity
            {
                TelegramChatId = chatId,
                TelegramUsername = username,
                CreatedAt = DateTime.UtcNow
            };
            db.Users.Add(user);
        }
        else if (username != null)
        {
            user.TelegramUsername = username;
        }

        await db.SaveChangesAsync(ct);
        return user;
    }

    public async Task UpdateCredentialsAsync(long chatId, string sessionCookie, string csrfToken, string? leetCodeUsername = null, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.TelegramChatId == chatId, ct)
            ?? throw new InvalidOperationException($"User with chat ID {chatId} not found");

        user.SessionCookie = sessionCookie;
        user.CsrfToken = csrfToken;
        user.CredentialsUpdatedAt = DateTime.UtcNow;
        
        if (leetCodeUsername != null)
        {
            user.LeetCodeUsername = leetCodeUsername;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateLastSuccessfulAuthAsync(long chatId, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.TelegramChatId == chatId, ct);
        if (user != null)
        {
            user.LastSuccessfulAuthAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task SetEnabledAsync(long chatId, bool enabled, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.TelegramChatId == chatId, ct);
        if (user != null)
        {
            user.IsEnabled = enabled;
            await db.SaveChangesAsync(ct);
        }
    }
}
