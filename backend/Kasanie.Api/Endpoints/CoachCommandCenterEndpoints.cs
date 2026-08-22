using System.Security.Claims;
using Kasanie.Api.Contracts;
using Kasanie.Api.Domain;
using Kasanie.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Kasanie.Api.Endpoints;

public static partial class EndpointMapping
{
    private static void MapCoachCommandCenter(RouteGroupBuilder coach)
    {
        coach.MapGet("/teams/{teamId:int}/command-center", async (int teamId, ClaimsPrincipal principal, AppDbContext db) =>
        {
            var team = await AssignedTeam(db, principal, teamId, false);
            if (team is null) return Results.Forbid();
            var now = DateTimeOffset.UtcNow;
            var players = await db.TeamPlayers.AsNoTracking().Where(x => x.TeamId == teamId && x.IsActive).OrderBy(x => x.ShirtNumber).ThenBy(x => x.Player.LastName).Select(x => new
            {
                x.PlayerId, x.Player.FirstName, x.Player.LastName, x.Player.DateOfBirth, x.Player.PreferredPosition, x.ShirtNumber,
                x.TournamentRegistrationStatus, x.CurrentSeasonPlan, x.NextSeasonPlan, x.TwoYearPlan,
                completedTrainings = db.TeamTrainingPlayerResults.Count(r => r.PlayerId == x.PlayerId && r.TeamTrainingExercise.TeamTraining.TeamId == teamId && r.IsCompleted),
                attendance = db.TeamTrainingAttendances.Count(a => a.PlayerId == x.PlayerId && a.TeamTraining.TeamId == teamId && (a.Status == AttendanceStatus.Present || a.Status == AttendanceStatus.Late)),
                activeInjury = db.TeamInjuries.Any(i => i.PlayerId == x.PlayerId && i.TeamId == teamId && i.Status != "Закрыта")
            }).ToListAsync();
            var messages = await db.TeamMessages.AsNoTracking().Where(x => x.TeamId == teamId).OrderByDescending(x => x.CreatedAt).Take(100).Select(x => new { x.Id, channel = x.Channel.ToString(), author = x.AuthorUser.Email, x.Text, x.CreatedAt }).ToListAsync();
            var injuries = await db.TeamInjuries.AsNoTracking().Where(x => x.TeamId == teamId).OrderByDescending(x => x.StartedOn).Select(x => new { x.Id, x.PlayerId, player = x.Player.FirstName + " " + x.Player.LastName, x.Type, x.Severity, x.Status, x.RiskLevel, x.StartedOn, x.ExpectedReturnOn, x.ClosedOn, x.Notes }).ToListAsync();
            var events = await db.TeamScheduleEvents.AsNoTracking().Where(x => x.TeamId == teamId).OrderBy(x => x.StartsAt).Select(x => new { x.Id, x.Type, x.Title, x.StartsAt, x.ReminderAt, x.Notes }).ToListAsync();
            var matches = await db.TeamMatches.AsNoTracking().Where(x => x.TeamId == teamId).OrderBy(x => x.ScheduledAt).Select(x => new { x.Id, x.Opponent, x.Competition, x.ScheduledAt, x.Venue, x.Status, x.GoalsFor, x.GoalsAgainst }).ToListAsync();
            var tournaments = await db.TeamTournaments.AsNoTracking().Where(x => x.TeamId == teamId).OrderBy(x => x.StartDate).Select(x => new { x.Id, x.Name, x.StartDate, x.EndDate, x.Status, x.SourceUrl, x.RegistrationDeadline }).ToListAsync();
            var trainings = await db.TeamTrainings.AsNoTracking().Where(x => x.TeamId == teamId).OrderByDescending(x => x.ScheduledAt).Take(30).Select(x => new { x.Id, x.Title, x.ScheduledAt, status = x.Status.ToString(), players = x.Attendances.Count, present = x.Attendances.Count(a => a.Status == AttendanceStatus.Present || a.Status == AttendanceStatus.Late) }).ToListAsync();
            return Results.Ok(new
            {
                team = new { team.Id, name = (team.AgeGroup ?? "") + (team.AgeGroup == null ? "" : " — ") + team.Name, team.Season, team.TrainingCycleStage, team.CycleStart, team.CycleEnd, team.TacticFormation, team.TacticNotes, team.TacticPlanJson, team.SetPiecesJson, team.OpponentInstructions, team.OpponentReportUrl, team.OpponentReportNotes, team.CodeOfConduct, school = team.School.Name },
                players, messages, injuries, events, matches, tournaments, trainings,
                summary = new { activeInjuries = injuries.Count(x => x.Status != "Закрыта"), highRiskPlayers = injuries.Where(x => x.Status != "Закрыта" && x.RiskLevel >= 70).Select(x => x.PlayerId).Distinct().Count(), nextMatch = matches.FirstOrDefault(x => x.ScheduledAt >= now), reminders = events.Count(x => x.StartsAt >= now && x.StartsAt <= now.AddDays(7)) }
            });
        });

        coach.MapPut("/teams/{teamId:int}/cycle", async (int teamId, TeamCycleRequest request, ClaimsPrincipal principal, AppDbContext db) =>
        {
            var team = await AssignedTeam(db, principal, teamId); if (team is null) return Results.Forbid();
            if (string.IsNullOrWhiteSpace(request.Stage) || request.StartsOn.HasValue && request.EndsOn.HasValue && request.StartsOn > request.EndsOn) return Results.ValidationProblem(new Dictionary<string, string[]> { ["cycle"] = ["Проверьте этап и даты тренировочного цикла."] });
            team.TrainingCycleStage = request.Stage.Trim(); team.CycleStart = request.StartsOn; team.CycleEnd = request.EndsOn; team.UpdatedAt = DateTimeOffset.UtcNow;
            await SaveCoachAudit(db, principal, "coach_team_cycle_updated", nameof(Team), teamId.ToString()); return Results.NoContent();
        });

        coach.MapPut("/teams/{teamId:int}/players/{playerId:int}", async (int teamId, int playerId, TeamPlayerManagementRequest request, ClaimsPrincipal principal, AppDbContext db) =>
        {
            if (await AssignedTeam(db, principal, teamId) is null) return Results.Forbid();
            var item = await db.TeamPlayers.SingleOrDefaultAsync(x => x.TeamId == teamId && x.PlayerId == playerId && x.IsActive); if (item is null) return Results.NotFound();
            if (request.ShirtNumber is < 1 or > 99) return Results.ValidationProblem(new Dictionary<string, string[]> { ["shirtNumber"] = ["Номер должен быть от 1 до 99."] });
            if (request.ShirtNumber.HasValue && await db.TeamPlayers.AnyAsync(x => x.TeamId == teamId && x.PlayerId != playerId && x.IsActive && x.ShirtNumber == request.ShirtNumber)) return Results.Conflict(new { message = "Этот номер уже занят в составе." });
            item.ShirtNumber = request.ShirtNumber; item.TournamentRegistrationStatus = request.TournamentRegistrationStatus.Trim(); item.CurrentSeasonPlan = request.CurrentSeasonPlan.Trim(); item.NextSeasonPlan = request.NextSeasonPlan.Trim(); item.TwoYearPlan = request.TwoYearPlan.Trim();
            await SaveCoachAudit(db, principal, "coach_team_player_planned", nameof(TeamPlayer), $"{teamId}:{playerId}"); return Results.NoContent();
        });

        coach.MapPut("/teams/{teamId:int}/collective", async (int teamId, TeamCollectiveRequest request, ClaimsPrincipal principal, AppDbContext db) =>
        {
            var team = await AssignedTeam(db, principal, teamId); if (team is null) return Results.Forbid(); team.CodeOfConduct = Clean(request.CodeOfConduct); await SaveCoachAudit(db, principal, "coach_team_code_updated", nameof(Team), teamId.ToString()); return Results.NoContent();
        });

        coach.MapPost("/teams/{teamId:int}/messages", async (int teamId, TeamMessageRequest request, ClaimsPrincipal principal, AppDbContext db) =>
        {
            if (await AssignedTeam(db, principal, teamId) is null) return Results.Forbid();
            if (!Enum.TryParse<TeamMessageChannel>(request.Channel, true, out var channel) || string.IsNullOrWhiteSpace(request.Text) || request.Text.Length > 2000) return Results.ValidationProblem(new Dictionary<string, string[]> { ["message"] = ["Выберите канал и введите сообщение до 2000 символов."] });
            var item = new TeamMessage { TeamId = teamId, AuthorUserId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!, Channel = channel, Text = request.Text.Trim() }; db.TeamMessages.Add(item); await SaveCoachAudit(db, principal, "coach_team_message_sent", nameof(TeamMessage), item.Id.ToString()); return Results.Ok(new { item.Id });
        });

        coach.MapPost("/teams/{teamId:int}/injuries", async (int teamId, TeamInjuryRequest request, ClaimsPrincipal principal, AppDbContext db) =>
        {
            if (await AssignedTeam(db, principal, teamId) is null) return Results.Forbid();
            if (!await db.TeamPlayers.AnyAsync(x => x.TeamId == teamId && x.PlayerId == request.PlayerId && x.IsActive)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["playerId"] = ["Игрок не входит в этот состав."] });
            if (string.IsNullOrWhiteSpace(request.Type) || request.RiskLevel is < 0 or > 100) return Results.ValidationProblem(new Dictionary<string, string[]> { ["injury"] = ["Проверьте тип травмы и риск от 0 до 100."] });
            var item = new TeamInjury { TeamId = teamId, PlayerId = request.PlayerId, Type = request.Type.Trim(), Severity = request.Severity.Trim(), Status = request.Status.Trim(), RiskLevel = request.RiskLevel, StartedOn = request.StartedOn, ExpectedReturnOn = request.ExpectedReturnOn, Notes = Clean(request.Notes) }; db.TeamInjuries.Add(item); await SaveCoachAudit(db, principal, "coach_team_injury_recorded", nameof(TeamInjury), item.Id.ToString()); return Results.Ok(new { item.Id });
        });

        coach.MapPost("/teams/{teamId:int}/events", async (int teamId, TeamScheduleEventRequest request, ClaimsPrincipal principal, AppDbContext db) =>
        {
            if (await AssignedTeam(db, principal, teamId) is null) return Results.Forbid();
            if (string.IsNullOrWhiteSpace(request.Title)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["title"] = ["Укажите название события."] });
            var item = new TeamScheduleEvent { TeamId = teamId, Type = string.IsNullOrWhiteSpace(request.Type) ? "Событие" : request.Type.Trim(), Title = request.Title.Trim(), StartsAt = request.StartsAt, ReminderAt = request.ReminderAt, Notes = Clean(request.Notes) }; db.TeamScheduleEvents.Add(item); await SaveCoachAudit(db, principal, "coach_team_event_created", nameof(TeamScheduleEvent), item.Id.ToString()); return Results.Ok(new { item.Id });
        });

        coach.MapPut("/teams/{teamId:int}/opponent-report", async (int teamId, TeamOpponentReportRequest request, ClaimsPrincipal principal, AppDbContext db) =>
        {
            var team = await AssignedTeam(db, principal, teamId); if (team is null) return Results.Forbid();
            if (!string.IsNullOrWhiteSpace(request.SourceUrl) && (!Uri.TryCreate(request.SourceUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["sourceUrl"] = ["Источник должен быть ссылкой https."] });
            team.OpponentReportUrl = Clean(request.SourceUrl); team.OpponentReportNotes = Clean(request.Notes); await SaveCoachAudit(db, principal, "coach_opponent_report_updated", nameof(Team), teamId.ToString()); return Results.NoContent();
        });
    }

    private static async Task<Team?> AssignedTeam(AppDbContext db, ClaimsPrincipal principal, int teamId, bool tracked = true)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var query = tracked ? db.Teams : db.Teams.AsNoTracking();
        return await query.Include(x => x.School).SingleOrDefaultAsync(x => x.Id == teamId && x.IsActive && x.School.IsActive && x.TeamCoaches.Any(c => c.Coach.UserId == userId));
    }

    private static async Task SaveCoachAudit(AppDbContext db, ClaimsPrincipal principal, string eventType, string entityType, string entityId)
    {
        db.AuditLogs.Add(new AuditLog { UserId = principal.FindFirstValue(ClaimTypes.NameIdentifier), EventType = eventType, EntityType = entityType, EntityId = entityId }); await db.SaveChangesAsync();
    }
}
