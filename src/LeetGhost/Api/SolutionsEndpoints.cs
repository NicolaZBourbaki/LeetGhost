using LeetGhost.Data.Entities;
using LeetGhost.Data.Repositories;
using LeetGhost.Data.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace LeetGhost.Api;

/// <summary>
/// API endpoints for solution management.
/// </summary>
public static class SolutionsEndpoints
{
    public static void MapSolutionsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/solutions")
            .WithTags("Solutions");

        group.MapGet("/", GetAllSolutions)
            .WithName("GetAllSolutions")
            .WithSummary("Get all solutions for a user")
            .WithDescription("Returns all stored solutions for the given user ID");

        group.MapGet("/{id:int}", GetSolutionById)
            .WithName("GetSolutionById")
            .WithSummary("Get solution by ID");

        group.MapPost("/", AddSolution)
            .WithName("AddSolution")
            .WithSummary("Add a new solution");

        group.MapDelete("/{id:int}", RemoveSolution)
            .WithName("RemoveSolution")
            .WithSummary("Remove a solution");

        group.MapPost("/{id:int}/toggle", ToggleSolution)
            .WithName("ToggleSolution")
            .WithSummary("Toggle solution enabled status");
    }

    private static async Task<IResult> GetAllSolutions(
        ISolutionRepository repo,
        [FromQuery] int userId,
        CancellationToken ct)
    {
        var solutions = await repo.GetAllForUserAsync(userId, ct);
        return Results.Ok(solutions);
    }

    private static async Task<IResult> GetSolutionById(
        ISolutionRepository repo,
        int id,
        CancellationToken ct)
    {
        var solution = await repo.GetByIdAsync(id, ct);
        return solution != null ? Results.Ok(solution) : Results.NotFound();
    }

    private static async Task<IResult> AddSolution(
        ISolutionRepository repo,
        AddSolutionRequest request,
        CancellationToken ct)
    {
        var solution = new SolutionEntity
        {
            UserId = request.UserId,
            ProblemSlug = request.ProblemSlug,
            ProblemTitle = request.ProblemTitle ?? request.ProblemSlug,
            Language = request.Language,
            Code = request.Code,
            Notes = request.Notes,
            AddedAt = DateTime.UtcNow
        };

        await repo.AddAsync(solution, ct);
        return Results.Created($"/api/solutions/{solution.Id}", solution);
    }

    private static async Task<IResult> RemoveSolution(
        ISolutionRepository repo,
        int id,
        CancellationToken ct)
    {
        var solution = await repo.GetByIdAsync(id, ct);
        if (solution == null)
            return Results.NotFound();

        await repo.DeleteAsync(id, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ToggleSolution(
        ISolutionRepository repo,
        int id,
        CancellationToken ct)
    {
        var solution = await repo.GetByIdAsync(id, ct);
        if (solution == null)
            return Results.NotFound();

        await repo.ToggleEnabledAsync(id, ct);
        return Results.Ok(new { enabled = !solution.IsEnabled });
    }
}

public record AddSolutionRequest(
    int UserId,
    string ProblemSlug,
    string? ProblemTitle,
    string Language,
    string Code,
    string? Notes
);
