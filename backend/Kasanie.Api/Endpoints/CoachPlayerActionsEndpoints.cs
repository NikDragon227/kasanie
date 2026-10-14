using System.Security.Claims;
using System.Data;
using System.Text.Json;
using Kasanie.Api.Contracts;
using Kasanie.Api.Domain;
using Kasanie.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Kasanie.Api.Endpoints;

public static partial class EndpointMapping
{
    public sealed record MoveSquadPlayerRequest(int DestinationTeamId);
    public sealed record QuickPlayerTrainingRequest(int? RestDays, string? Intensity, string? Notes, int UtcOffsetMinutes = 0);

    private static void MapCoachPlayerActions(RouteGroupBuilder coach)
    {
        coach.MapGet("/teams/{teamId:int}/players/{playerId:int}/actions", async (int teamId, int playerId, ClaimsPrincipal principal, AppDbContext db) =>
        {
            var team = await AssignedTeam(db, principal, teamId, false);
            if (team is null || !await db.TeamPlayers.AnyAsync(x => x.TeamId == teamId && x.PlayerId == playerId && x.IsActive)) return Results.Forbid();
            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var teams = await db.Teams.AsNoTracking().Where(x => x.Id != teamId && x.SchoolId == team.SchoolId && x.IsActive && x.TeamCoaches.Any(c => c.Coach.UserId == userId)
                && !x.TeamPlayers.Any(p => p.PlayerId == playerId && p.IsActive))
                .Select(x => new { x.Id, name = (x.AgeGroup ?? "") + (x.AgeGroup == null ? "" : " — ") + x.Name }).ToListAsync();
            var members = await db.TeamPlayers.AsNoTracking().Where(x => x.IsActive && x.Team.IsActive && x.Team.School.IsActive && x.Team.TeamCoaches.Any(c => c.Coach.UserId == userId))
                .Include(x => x.Player).Include(x => x.Team).OrderBy(x => x.Player.LastName).ToListAsync();
            var ids = members.Select(x => x.PlayerId).Distinct().ToList();
            var snapshots = await db.SkillSnapshots.AsNoTracking().Where(x => ids.Contains(x.PlayerId)).OrderByDescending(x => x.CapturedAt).ToListAsync();
            var players = members.GroupBy(x => x.PlayerId).Select(group =>
            {
                var row = group.FirstOrDefault(x => x.TeamId == teamId) ?? group.First();
                var p = row.Player;
                var latest = snapshots.FirstOrDefault(x => x.PlayerId == p.Id);
                return new { playerId = p.Id, p.FirstName, p.LastName, p.DateOfBirth, p.PreferredPosition, p.DominantFoot, p.ExperienceLevel, p.Height, p.Weight,
                    row.Appearances, row.Goals, row.Assists, row.AverageRating, inCurrentTeam = group.Any(x => x.TeamId == teamId),
                    teams = group.Select(x => (x.Team.AgeGroup ?? "") + " — " + x.Team.Name).ToList(),
                    skills = latest is null ? null : SkillsDto(latest), skillsCapturedAt = latest?.CapturedAt };
            }).ToList();
            var workspace = await db.TeamTrainingWorkspaces.AsNoTracking().SingleOrDefaultAsync(x => x.TeamId == teamId);
            return Results.Ok(new { teams, players, training = ReadTrainingState(workspace).Players.FirstOrDefault(x => x.PlayerId == playerId) });
        });

        coach.MapPut("/teams/{teamId:int}/players/{playerId:int}/training", async (int teamId, int playerId, QuickPlayerTrainingRequest request, ClaimsPrincipal principal, AppDbContext db) =>
        {
            if (await AssignedTeam(db, principal, teamId, false) is null) return Results.Forbid();
            var player = await db.TeamPlayers.AsNoTracking().Include(x => x.Player).SingleOrDefaultAsync(x => x.TeamId == teamId && x.PlayerId == playerId && x.IsActive);
            if (player is null) return Results.NotFound();
            if (request.RestDays is < 0 or > 30 || request.Intensity is not null && !TrainingIntensities.Contains(request.Intensity) || request.Notes?.Length > 2000 || request.UtcOffsetMinutes is < -840 or > 840
                || request.RestDays is null && request.Intensity is null && string.IsNullOrWhiteSpace(request.Notes))
                return Results.BadRequest(new { message = "Проверьте отдых, интенсивность и заметку (до 2000 символов)." });
            var workspace = await db.TeamTrainingWorkspaces.SingleOrDefaultAsync(x => x.TeamId == teamId);
            if (workspace is null) { workspace = new TeamTrainingWorkspace { TeamId = teamId }; db.TeamTrainingWorkspaces.Add(workspace); }
            var state = ReadTrainingState(workspace);
            var settings = state.Players.FirstOrDefault(x => x.PlayerId == playerId) ?? DefaultPlayerTrainingSettings(player.Player);
            if (request.RestDays.HasValue) settings = settings with { Intensity = "Normal", RestUntil = request.RestDays == 0 ? null : DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromMinutes(request.UtcOffsetMinutes)).DateTime).AddDays(request.RestDays.Value - 1) };
            if (request.Intensity is not null) settings = settings with { Intensity = request.Intensity, RestUntil = null };
            if (!string.IsNullOrWhiteSpace(request.Notes)) settings = settings with { Notes = string.IsNullOrWhiteSpace(settings.Notes) ? request.Notes.Trim() : settings.Notes + "\n" + request.Notes.Trim() };
            if (settings.Notes?.Length > 2000) return Results.BadRequest(new { message = "Суммарная заметка превышает 2000 символов. Отредактируйте её в индивидуальных тренировках." });
            state.Players.RemoveAll(x => x.PlayerId == playerId); state.Players.Add(settings);
            workspace.StateJson = JsonSerializer.Serialize(state, TrainingJson); workspace.Version++; workspace.UpdatedAt = DateTimeOffset.UtcNow;
            db.AuditLogs.Add(new AuditLog { UserId = principal.FindFirstValue(ClaimTypes.NameIdentifier), EventType = "player_training_settings_updated", EntityType = nameof(TeamPlayer), EntityId = $"{teamId}:{playerId}" });
            try { await db.SaveChangesAsync(); }
            catch (DbUpdateException) { return Results.Conflict(new { message = "План изменился параллельно. Обновите состав и повторите действие." }); }
            return Results.Ok(new { training = settings, workspace.Version });
        });

        coach.MapPost("/teams/{teamId:int}/players/{playerId:int}/move", async (int teamId, int playerId, MoveSquadPlayerRequest request, ClaimsPrincipal principal, AppDbContext db) =>
        {
            var source = await AssignedTeam(db, principal, teamId, false);
            var destination = await AssignedTeam(db, principal, request.DestinationTeamId, false);
            if (source is null || destination is null) return Results.Forbid();
            if (source.Id == destination.Id || source.SchoolId != destination.SchoolId) return Results.BadRequest(new { message = "Выберите другой доступный состав этой же школы." });
            await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable) : null;
            var membership = await db.TeamPlayers.SingleOrDefaultAsync(x => x.TeamId == teamId && x.PlayerId == playerId && x.IsActive);
            if (membership is null) return Results.NotFound();
            var target = await db.TeamPlayers.SingleOrDefaultAsync(x => x.TeamId == destination.Id && x.PlayerId == playerId);
            if (target?.IsActive == true) return Results.Conflict(new { message = "Игрок уже в этом составе." });
            var number = membership.ShirtNumber;
            if (number.HasValue && await db.TeamPlayers.AnyAsync(x => x.TeamId == destination.Id && x.IsActive && x.ShirtNumber == number)) number = null;
            membership.IsActive = false; membership.IsCaptain = false; membership.LeftAt = DateTimeOffset.UtcNow;
            if (target is null) { target = new TeamPlayer { TeamId = destination.Id, PlayerId = playerId }; db.TeamPlayers.Add(target); }
            target.IsActive = true; target.LeftAt = null; target.ShirtNumber = number; target.JoinedAt = DateTimeOffset.UtcNow;
            var sourceWorkspace = await db.TeamTrainingWorkspaces.SingleOrDefaultAsync(x => x.TeamId == teamId);
            if (sourceWorkspace is not null)
            {
                var state = ReadTrainingState(sourceWorkspace);
                var individual = state.Players.FirstOrDefault(x => x.PlayerId == playerId);
                state.Players.RemoveAll(x => x.PlayerId == playerId);
                state.Mentoring = state.Mentoring.Select(x => x with { PlayerIds = x.PlayerIds.Where(id => id != playerId).ToList(), MentorId = x.MentorId == playerId ? null : x.MentorId }).ToList();
                sourceWorkspace.StateJson = JsonSerializer.Serialize(state, TrainingJson); sourceWorkspace.Version++; sourceWorkspace.UpdatedAt = DateTimeOffset.UtcNow;
                if (individual is not null)
                {
                    var targetWorkspace = await db.TeamTrainingWorkspaces.SingleOrDefaultAsync(x => x.TeamId == destination.Id);
                    if (targetWorkspace is null) { targetWorkspace = new TeamTrainingWorkspace { TeamId = destination.Id }; db.TeamTrainingWorkspaces.Add(targetWorkspace); }
                    var targetState = ReadTrainingState(targetWorkspace); targetState.Players.RemoveAll(x => x.PlayerId == playerId); targetState.Players.Add(individual);
                    targetWorkspace.StateJson = JsonSerializer.Serialize(targetState, TrainingJson); targetWorkspace.Version++; targetWorkspace.UpdatedAt = DateTimeOffset.UtcNow;
                }
            }
            db.AuditLogs.Add(new AuditLog { UserId = principal.FindFirstValue(ClaimTypes.NameIdentifier), EventType = "squad_player_moved", EntityType = nameof(TeamPlayer), EntityId = $"{teamId}:{playerId}", Details = $"destination={destination.Id}" });
            try { await db.SaveChangesAsync(); if (transaction is not null) await transaction.CommitAsync(); }
            catch (DbUpdateException) { return Results.Conflict(new { message = "Состав изменился параллельно. Обновите данные и повторите перемещение." }); }
            catch (Npgsql.PostgresException exception) when (exception.SqlState is "40001" or "40P01") { return Results.Conflict(new { message = "Игрок перемещается другим тренером. Обновите состав." }); }
            return Results.Ok(new { destinationTeamId = destination.Id, shirtNumber = number });
        });
    }

    private static TrainingPlayerSettings DefaultPlayerTrainingSettings(PlayerProfile player) => new(player.Id, player.PreferredPosition == "Вратарь" ? "Goalkeepers" : player.PreferredPosition == "Защитник" ? "Defence" : "Attack", player.PreferredPosition, "Игровая позиция", "Не задано", "Normal", false, null, null);
}
