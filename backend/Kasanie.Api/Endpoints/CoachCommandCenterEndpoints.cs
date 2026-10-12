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
            var coachName = await db.TeamCoaches.AsNoTracking().Where(x => x.TeamId == teamId).OrderByDescending(x => x.IsHeadCoach).ThenBy(x => x.CreatedAt).Select(x => x.Coach.DisplayName).FirstOrDefaultAsync();
            var players = await db.TeamPlayers.AsNoTracking().Where(x => x.TeamId == teamId && x.IsActive).OrderBy(x => x.ShirtNumber).ThenBy(x => x.Player.LastName).Select(x => new
            {
                x.PlayerId, x.Player.FirstName, x.Player.LastName, x.Player.DateOfBirth, x.Player.Gender, x.Player.PreferredPosition, x.Player.DominantFoot, x.Player.ExperienceLevel, isRegistered = x.Player.UserId != null, x.ShirtNumber,
                x.Player.Height, x.Player.Weight, x.Player.Citizenship, x.Player.SecondCitizenship, x.IsCaptain, x.Appearances, x.Goals, x.Assists, x.AverageRating,
                x.TournamentRegistrationStatus, x.CurrentSeasonPlan, x.NextSeasonPlan, x.TwoYearPlan,
                completedTrainings = db.TeamTrainingPlayerResults.Count(r => r.PlayerId == x.PlayerId && r.TeamTrainingExercise.TeamTraining.TeamId == teamId && r.IsCompleted),
                attendance = db.TeamTrainingAttendances.Count(a => a.PlayerId == x.PlayerId && a.TeamTraining.TeamId == teamId && (a.Status == AttendanceStatus.Present || a.Status == AttendanceStatus.Late)),
                activeInjury = db.TeamInjuries.Any(i => i.PlayerId == x.PlayerId && i.TeamId == teamId && i.Status != "Закрыта")
            }).ToListAsync();
            var messages = await db.TeamMessages.AsNoTracking().Where(x => x.TeamId == teamId).OrderByDescending(x => x.CreatedAt).Take(100).Select(x => new { x.Id, channel = x.Channel.ToString(), author = x.AuthorUser.Email, x.Text, x.CreatedAt }).ToListAsync();
            var injuries = await db.TeamInjuries.AsNoTracking().Where(x => x.TeamId == teamId).OrderByDescending(x => x.StartedOn).Select(x => new { x.Id, x.PlayerId, player = x.Player.FirstName + " " + x.Player.LastName, x.Type, x.Severity, x.Status, x.RiskLevel, x.StartedOn, x.ExpectedReturnOn, x.ClosedOn, x.Notes }).ToListAsync();
            var events = await db.TeamScheduleEvents.AsNoTracking().Where(x => x.TeamId == teamId).OrderBy(x => x.StartsAt).Select(x => new { x.Id, x.Type, x.Title, x.StartsAt, x.ReminderAt, x.Notes }).ToListAsync();
            var matches = await db.TeamMatches.AsNoTracking().Where(x => x.TeamId == teamId).OrderBy(x => x.ScheduledAt).Select(x => new { x.Id, x.Opponent, x.Competition, x.ScheduledAt, x.Venue, x.Status, x.GoalsFor, x.GoalsAgainst }).ToListAsync();
            var tournaments = await db.TeamTournaments.AsNoTracking().Where(x => x.TeamId == teamId).OrderBy(x => x.StartDate).Select(x => new { x.Id, x.Name, x.StartDate, x.EndDate, x.Status, x.SourceUrl, x.RegistrationDeadline, x.MaxRosterSize, x.RegistrationRequirements, registeredPlayerIds = x.Registrations.Select(registration => registration.PlayerId).ToList() }).ToListAsync();
            var trainings = await db.TeamTrainings.AsNoTracking().Where(x => x.TeamId == teamId).OrderByDescending(x => x.ScheduledAt).Take(30).Select(x => new { x.Id, x.Title, x.ScheduledAt, status = x.Status.ToString(), players = x.Attendances.Count, present = x.Attendances.Count(a => a.Status == AttendanceStatus.Present || a.Status == AttendanceStatus.Late) }).ToListAsync();
            var invitations = await db.TeamInvitations.AsNoTracking().Where(x => x.TeamId == teamId).OrderByDescending(x => x.CreatedAt).Take(30).Select(x => new
            {
                x.Id, x.PlayerId, x.Player.FirstName, x.Player.LastName, x.Player.DateOfBirth, x.Player.PreferredPosition, x.Player.DominantFoot,
                status = x.Status.ToString(), x.CreatedAt, x.RespondedAt
            }).ToListAsync();
            return Results.Ok(new
            {
                team = new { team.Id, name = (team.AgeGroup ?? "") + (team.AgeGroup == null ? "" : " — ") + team.Name, team.Season, team.TrainingCycleStage, team.CycleStart, team.CycleEnd, team.TacticFormation, team.TacticNotes, team.TacticPlanJson, team.SetPiecesJson, team.OpponentInstructions, team.OpponentReportUrl, team.OpponentReportNotes, team.CodeOfConduct, school = team.School.Name, coachName },
                players, invitations, messages, injuries, events, matches, tournaments, trainings,
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

        coach.MapPut("/teams/{teamId:int}/captain", async (int teamId, TeamCaptainRequest request, ClaimsPrincipal principal, AppDbContext db) =>
        {
            if (await AssignedTeam(db, principal, teamId, false) is null) return Results.Forbid();
            var members = await db.TeamPlayers.Where(x => x.TeamId == teamId && x.IsActive).ToListAsync();
            var captain = request.PlayerId is null ? null : members.SingleOrDefault(x => x.PlayerId == request.PlayerId);
            if (request.PlayerId is not null && captain is null) return Results.ValidationProblem(new Dictionary<string, string[]> { ["playerId"] = ["Капитан должен быть игроком этой команды."] });
            foreach (var member in members.Where(x => x.IsCaptain)) member.IsCaptain = false;
            await db.SaveChangesAsync();
            if (captain is not null) captain.IsCaptain = true;
            await SaveCoachAudit(db, principal, "coach_team_captain_updated", nameof(TeamPlayer), $"{teamId}:{request.PlayerId}");
            return Results.NoContent();
        });

        coach.MapPut("/teams/{teamId:int}/tournaments/{tournamentId:int}/registration-settings", async (int teamId, int tournamentId, TeamTournamentRegistrationSettingsRequest request, ClaimsPrincipal principal, AppDbContext db) =>
        {
            if (await AssignedTeam(db, principal, teamId, false) is null) return Results.Forbid();
            var tournament = await db.TeamTournaments.SingleOrDefaultAsync(x => x.Id == tournamentId && x.TeamId == teamId);
            if (tournament is null) return Results.NotFound();
            if (request.MaxRosterSize is < 1 or > 99) return Results.ValidationProblem(new Dictionary<string, string[]> { ["maxRosterSize"] = ["Укажите лимит от 1 до 99 игроков."] });
            if (request.Requirements?.Length > 2000) return Results.ValidationProblem(new Dictionary<string, string[]> { ["requirements"] = ["Условия не должны превышать 2000 символов."] });
            tournament.MaxRosterSize = request.MaxRosterSize;
            tournament.RegistrationRequirements = Clean(request.Requirements);
            await SaveCoachAudit(db, principal, "coach_tournament_registration_settings_updated", nameof(TeamTournament), tournamentId.ToString());
            return Results.NoContent();
        });

        coach.MapPut("/teams/{teamId:int}/tournaments/{tournamentId:int}/registrations", async (int teamId, int tournamentId, TeamTournamentRegistrationsRequest request, ClaimsPrincipal principal, AppDbContext db) =>
        {
            if (await AssignedTeam(db, principal, teamId, false) is null) return Results.Forbid();
            var tournament = await db.TeamTournaments.SingleOrDefaultAsync(x => x.Id == tournamentId && x.TeamId == teamId);
            if (tournament is null) return Results.NotFound();
            var playerIds = request.PlayerIds.Distinct().ToList();
            if (tournament.MaxRosterSize.HasValue && playerIds.Count > tournament.MaxRosterSize.Value) return Results.ValidationProblem(new Dictionary<string, string[]> { ["playerIds"] = ["В заявке больше игроков, чем допускает лимит турнира."] });
            var validIds = await db.TeamPlayers.Where(x => x.TeamId == teamId && x.IsActive && playerIds.Contains(x.PlayerId)).Select(x => x.PlayerId).ToListAsync();
            if (validIds.Count != playerIds.Count) return Results.ValidationProblem(new Dictionary<string, string[]> { ["playerIds"] = ["В заявку можно включать только игроков текущего состава."] });
            var existing = await db.TeamTournamentRegistrations.Where(x => x.TeamTournamentId == tournamentId).ToListAsync();
            db.TeamTournamentRegistrations.RemoveRange(existing);
            db.TeamTournamentRegistrations.AddRange(playerIds.Select(playerId => new TeamTournamentRegistration { TeamTournamentId = tournamentId, PlayerId = playerId }));
            await SaveCoachAudit(db, principal, "coach_tournament_registration_updated", nameof(TeamTournament), tournamentId.ToString());
            return Results.NoContent();
        });

        coach.MapPost("/teams/{teamId:int}/players", async (int teamId, CoachTeamPlayerCreateRequest request, ClaimsPrincipal principal, AppDbContext db) =>
        {
            if (await AssignedTeam(db, principal, teamId) is null) return Results.Forbid();
            var errors = CoachPlayerErrors(request.FirstName, request.LastName, request.DateOfBirth, request.PreferredPosition, request.DominantFoot, request.ExperienceLevel, request.ShirtNumber);
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            if (request.ShirtNumber.HasValue && await db.TeamPlayers.AnyAsync(x => x.TeamId == teamId && x.IsActive && x.ShirtNumber == request.ShirtNumber))
                return Results.Conflict(new { message = "Этот номер уже занят в составе." });

            var player = new PlayerProfile
            {
                FirstName = request.FirstName.Trim(), LastName = request.LastName.Trim(), DateOfBirth = request.DateOfBirth,
                Gender = Clean(request.Gender), PreferredPosition = request.PreferredPosition.Trim(), DominantFoot = request.DominantFoot.Trim(), ExperienceLevel = request.ExperienceLevel.Trim(),
                Height = request.Height, Weight = request.Weight, Citizenship = Clean(request.Citizenship), SecondCitizenship = Clean(request.SecondCitizenship)
            };
            db.Players.Add(player);
            db.TeamPlayers.Add(new TeamPlayer { TeamId = teamId, Player = player, ShirtNumber = request.ShirtNumber });
            await db.SaveChangesAsync();
            db.AuditLogs.Add(new AuditLog { UserId = principal.FindFirstValue(ClaimTypes.NameIdentifier), EventType = "coach_team_player_created", EntityType = nameof(PlayerProfile), EntityId = player.Id.ToString(), Details = $"team={teamId}" });
            await db.SaveChangesAsync();
            return Results.Created($"/api/coach/teams/{teamId}/players/{player.Id}", new { player.Id });
        });

        coach.MapGet("/teams/{teamId:int}/registered-players", async (int teamId, string? search, ClaimsPrincipal principal, AppDbContext db) =>
        {
            var team = await AssignedTeam(db, principal, teamId, false);
            if (team is null) return Results.Forbid();
            if (string.IsNullOrWhiteSpace(search) || search.Trim().Length < 2) return Results.Ok(Array.Empty<object>());
            var term = search.Trim().ToLower();
            return Results.Ok(await db.Players.AsNoTracking()
                .Where(x => x.UserId != null && (x.FirstName + " " + x.LastName).ToLower().Contains(term) && !x.TeamPlayers.Any(link => link.TeamId == teamId && link.IsActive) && !x.TeamPlayers.Any(link => link.IsActive && link.Team.SchoolId != team.SchoolId) && !x.TeamInvitations.Any(invitation => invitation.TeamId == teamId && invitation.Status == TeamInvitationStatus.Pending))
                .OrderBy(x => x.LastName).ThenBy(x => x.FirstName).Take(12)
                .Select(x => new { x.Id, x.FirstName, x.LastName, x.DateOfBirth, x.Gender, x.PreferredPosition, x.DominantFoot, x.ExperienceLevel })
                .ToListAsync());
        });

        coach.MapPost("/teams/{teamId:int}/invitations", async (int teamId, TeamInvitationRequest request, ClaimsPrincipal principal, AppDbContext db) =>
        {
            var team = await AssignedTeam(db, principal, teamId, false);
            if (team is null) return Results.Forbid();
            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var coachProfile = await db.CoachProfiles.SingleAsync(x => x.UserId == userId);
            var player = await db.Players.SingleOrDefaultAsync(x => x.Id == request.PlayerId && x.UserId != null);
            if (player is null) return Results.ValidationProblem(new Dictionary<string, string[]> { ["playerId"] = ["Игрок должен иметь подтверждённый личный кабинет."] });
            if (await db.TeamPlayers.AnyAsync(x => x.TeamId == teamId && x.PlayerId == player.Id && x.IsActive)) return Results.Conflict(new { message = "Этот игрок уже в составе." });
            if (await db.TeamPlayers.AnyAsync(x => x.PlayerId == player.Id && x.IsActive && x.Team.SchoolId != team.SchoolId)) return Results.Conflict(new { message = "Игрок уже состоит в другой школе." });

            var invitation = await db.TeamInvitations.SingleOrDefaultAsync(x => x.TeamId == teamId && x.PlayerId == player.Id);
            if (invitation is not null && invitation.Status == TeamInvitationStatus.Pending) return Results.Conflict(new { message = "Приглашение уже ожидает ответа игрока." });
            if (invitation is null) { invitation = new TeamInvitation { TeamId = teamId, PlayerId = player.Id, InvitedByCoachId = coachProfile.Id }; db.TeamInvitations.Add(invitation); }
            else { invitation.InvitedByCoachId = coachProfile.Id; invitation.Status = TeamInvitationStatus.Pending; invitation.CreatedAt = DateTimeOffset.UtcNow; invitation.RespondedAt = null; }
            await db.SaveChangesAsync();
            db.AuditLogs.Add(new AuditLog { UserId = userId, EventType = "coach_team_invitation_sent", EntityType = nameof(TeamInvitation), EntityId = invitation.Id.ToString(), Details = $"team={teamId};player={player.Id}" });
            await db.SaveChangesAsync();
            return Results.Created($"/api/coach/teams/{teamId}/invitations/{invitation.Id}", new { invitation.Id });
        });

        coach.MapDelete("/teams/{teamId:int}/invitations/{invitationId:int}", async (int teamId, int invitationId, ClaimsPrincipal principal, AppDbContext db) =>
        {
            if (await AssignedTeam(db, principal, teamId, false) is null) return Results.Forbid();
            var invitation = await db.TeamInvitations.SingleOrDefaultAsync(x => x.Id == invitationId && x.TeamId == teamId && x.Status == TeamInvitationStatus.Pending);
            if (invitation is null) return Results.NotFound();
            invitation.Status = TeamInvitationStatus.Cancelled; invitation.RespondedAt = DateTimeOffset.UtcNow;
            await SaveCoachAudit(db, principal, "coach_team_invitation_cancelled", nameof(TeamInvitation), invitationId.ToString());
            return Results.NoContent();
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

    private static Dictionary<string, string[]> CoachPlayerErrors(string firstName, string lastName, DateOnly dateOfBirth, string position, string foot, string level, int? shirtNumber)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(firstName) || firstName.Trim().Length > 80) errors["firstName"] = ["Укажите имя до 80 символов."];
        if (string.IsNullOrWhiteSpace(lastName) || lastName.Trim().Length > 80) errors["lastName"] = ["Укажите фамилию до 80 символов."];
        if (dateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow) || dateOfBirth < new DateOnly(1900, 1, 1)) errors["dateOfBirth"] = ["Укажите корректную дату рождения."];
        if (string.IsNullOrWhiteSpace(position) || string.IsNullOrWhiteSpace(foot) || string.IsNullOrWhiteSpace(level)) errors["profile"] = ["Заполните игровую позицию, ведущую ногу и уровень."];
        if (shirtNumber is < 1 or > 99) errors["shirtNumber"] = ["Номер должен быть от 1 до 99."];
        return errors;
    }
}
