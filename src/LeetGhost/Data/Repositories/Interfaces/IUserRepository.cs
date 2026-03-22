using LeetGhost.Data.Entities;

namespace LeetGhost.Data.Repositories.Interfaces;

/// <summary>
/// Repository for user management.
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Gets a user by Telegram chat ID.
    /// </summary>
    Task<UserEntity?> GetByTelegramChatIdAsync(long chatId, CancellationToken ct = default);

    /// <summary>
    /// Gets all users with valid credentials.
    /// </summary>
    Task<IReadOnlyList<UserEntity>> GetAllActiveUsersAsync(CancellationToken ct = default);

    /// <summary>
    /// Creates or updates a user.
    /// </summary>
    Task<UserEntity> UpsertAsync(long chatId, string? username = null, CancellationToken ct = default);

    /// <summary>
    /// Updates user credentials.
    /// </summary>
    Task UpdateCredentialsAsync(long chatId, string sessionCookie, string csrfToken, string? leetCodeUsername = null, CancellationToken ct = default);

    /// <summary>
    /// Updates last successful auth timestamp.
    /// </summary>
    Task UpdateLastSuccessfulAuthAsync(long chatId, CancellationToken ct = default);

    /// <summary>
    /// Toggles user enabled status.
    /// </summary>
    Task SetEnabledAsync(long chatId, bool enabled, CancellationToken ct = default);
}
