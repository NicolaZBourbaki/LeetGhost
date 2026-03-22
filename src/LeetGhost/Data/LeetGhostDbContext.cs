using LeetGhost.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LeetGhost.Data;

/// <summary>
/// Entity Framework DbContext for LeetGhost.
/// </summary>
public class LeetGhostDbContext(DbContextOptions<LeetGhostDbContext> options) : DbContext(options)
{
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<SolutionEntity> Solutions => Set<SolutionEntity>();
    public DbSet<SubmissionLogEntity> SubmissionLogs => Set<SubmissionLogEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User configuration
        modelBuilder.Entity<UserEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TelegramChatId).IsUnique();
            entity.Property(e => e.TelegramChatId).IsRequired();
            entity.Property(e => e.TimeZone).HasDefaultValue("UTC");
        });

        // Solution configuration
        modelBuilder.Entity<SolutionEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.ProblemSlug, e.Language });
            
            entity.HasOne(e => e.User)
                .WithMany(u => u.Solutions)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(e => e.ProblemSlug).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Language).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Code).IsRequired();
        });

        // SubmissionLog configuration
        modelBuilder.Entity<SubmissionLogEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.SubmittedAt });
            
            entity.HasOne(e => e.User)
                .WithMany(u => u.SubmissionLogs)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Solution)
                .WithMany()
                .HasForeignKey(e => e.SolutionId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.Property(e => e.ProblemSlug).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Status).HasMaxLength(50);
        });
    }
}
