using LeetGhost.Api;
using LeetGhost.Configuration;
using LeetGhost.Data;
using LeetGhost.Data.Repositories;
using LeetGhost.Data.Repositories.Interfaces;
using LeetGhost.Services;
using LeetGhost.Telegram;
using LeetGhost.Workers;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Services.Configure<ScheduleSettings>(
    builder.Configuration.GetSection(ScheduleSettings.SectionName));
builder.Services.Configure<TelegramBotSettings>(
    builder.Configuration.GetSection(TelegramBotSettings.SectionName));

// SQLite Database
builder.Services.AddDbContext<LeetGhostDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ISolutionRepository, SolutionRepository>();
builder.Services.AddScoped<ISubmissionLogRepository, SubmissionLogRepository>();

// Services
builder.Services.AddHttpClient();
builder.Services.AddScoped<LeetCodeApiService>();

// Telegram Bot (singleton for sending notifications)
builder.Services.AddSingleton<TelegramBotService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<TelegramBotService>());

// Streak Keeper Worker
builder.Services.AddHostedService<StreakKeeperWorker>();

// Add OpenAPI/Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() 
    { 
        Title = "LeetGhost API", 
        Version = "v1",
        Description = "LeetCode Streak Keeper - Automatic submission service"
    });
});

var app = builder.Build();

// Ensure database is created and migrated
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LeetGhostDbContext>();
    await db.Database.EnsureCreatedAsync();
    
    // Apply schema migrations for new columns
    await ApplyCustomMigrationsAsync(db);
}

// Configure the HTTP request pipeline
// Enable Swagger in all environments for API documentation
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "LeetGhost API v1");
    c.RoutePrefix = "swagger";
});

// Map API endpoints
app.MapHealthEndpoints();
app.MapSolutionsEndpoints();

// Startup banner
Console.WriteLine(@"
╔═══════════════════════════════════════════════════════════╗
║                                                           ║
║   ██╗     ███████╗███████╗████████╗ ██████╗ ██╗  ██╗     ║
║   ██║     ██╔════╝██╔════╝╚══██╔══╝██╔════╝ ██║  ██║     ║
║   ██║     █████╗  █████╗     ██║   ██║  ███╗███████║     ║
║   ██║     ██╔══╝  ██╔══╝     ██║   ██║   ██║██╔══██║     ║
║   ███████╗███████╗███████╗   ██║   ╚██████╔╝██║  ██║     ║
║   ╚══════╝╚══════╝╚══════╝   ╚═╝    ╚═════╝ ╚═╝  ╚═╝     ║
║                                                           ║
║              🔥 LeetCode Streak Keeper 🔥                 ║
║                                                           ║
║   Control via Telegram Bot                                ║
║   API: http://localhost:5000                              ║
║                                                           ║
╚═══════════════════════════════════════════════════════════╝
");

await app.RunAsync();

static async Task ApplyCustomMigrationsAsync(LeetGhostDbContext db)
{
    var migrations = new List<(string Table, string Column, string Definition)>
    {
        // Add new columns here as the schema evolves
        ("Solutions", "IsSubmittedToLeetCode", "INTEGER NOT NULL DEFAULT 0"),
    };

    foreach (var (table, column, definition) in migrations)
    {
        if (!await ColumnExistsAsync(db, table, column))
        {
            var sql = $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {definition}";
            await db.Database.ExecuteSqlRawAsync(sql);
            Console.WriteLine($"[Migration] Added column {table}.{column}");
        }
    }
}

static async Task<bool> ColumnExistsAsync(LeetGhostDbContext db, string tableName, string columnName)
{
    var connection = db.Database.GetDbConnection();
    await connection.OpenAsync();
    
    try
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{tableName}\")";

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var name = reader.GetString(1); // Column name is at index 1
            if (name.Equals(columnName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
    finally
    {
        await connection.CloseAsync();
    }
}
