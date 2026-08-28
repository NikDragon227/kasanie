using Microsoft.AspNetCore.Identity;

namespace Kasanie.Api.Domain;

public static class Roles
{
    public const string Player = "Player";
    public const string Coach = "Coach";
    public const string Parent = "Parent";
    public const string RegionalAnalyst = "RegionalAnalyst";
    public const string SchoolOwner = "SchoolOwner";
    public const string SchoolAdmin = "SchoolAdmin";
    public const string Organizer = "Organizer";
    public const string Admin = "Admin";
    public static readonly string[] All = [Player, Coach, Parent, RegionalAnalyst, SchoolOwner, SchoolAdmin, Organizer, Admin];
}

public static class KasanieClaimTypes
{
    public const string AnalyticsRegion = "kasanie:analytics-region";
}

public enum SkillCategory { Speed, Endurance, BallControl, Passing, Shooting, Agility }
public enum ScoringDirection { HigherIsBetter, LowerIsBetter }
public enum LinkStatus { Pending, Active, Suspended }
public enum PlanStatus { Active, Completed, Archived }
public enum SessionStatus { Planned, InProgress, Completed }
public enum SchoolMembershipRole { Owner, Administrator, Coach }
public enum TeamTrainingStatus { Planned, InProgress, Completed }
public enum AttendanceStatus { Unknown, Present, Late, Absent, Excused }
public enum TeamMessageChannel { Owner, Team, Parents }
public enum PublicActivityType { Game, GroupTraining, CoachTraining, OpenTeamTraining, TrainingPartner, PlayerRecruitment, RecurringGroup, Tournament, Trial, OpenPractice }
public enum PublicActivityStatus { Draft, Published, Full, Cancelled, Completed, Archived }
public enum PublicActivityVisibility { Public, LinkOnly, CommunityOnly, Private }
public enum PublicParticipantStatus { Pending, Confirmed, Waitlisted, Cancelled, Attended, NoShow, Rejected }

public sealed class ApplicationUser : IdentityUser
{
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastActiveAt { get; set; }
}

public sealed class Municipality
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Region { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class School
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public string? City { get; set; }
    public string? ContactEmail { get; set; }
    public string? Phone { get; set; }
    public string? LogoUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class SchoolMembership
{
    public int SchoolId { get; set; }
    public School School { get; set; } = null!;
    public required string UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public SchoolMembershipRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Team
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public School School { get; set; } = null!;
    public required string Name { get; set; }
    public string? AgeGroup { get; set; }
    public string? Season { get; set; }
    public string TrainingCycleStage { get; set; } = "Подготовительный этап";
    public DateOnly? CycleStart { get; set; }
    public DateOnly? CycleEnd { get; set; }
    public string? TacticFormation { get; set; }
    public string? TacticNotes { get; set; }
    public string? TacticPlanJson { get; set; }
    public string? SetPiecesJson { get; set; }
    public string? OpponentInstructions { get; set; }
    public string? OpponentReportUrl { get; set; }
    public string? OpponentReportNotes { get; set; }
    public string? CodeOfConduct { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<TeamCoach> TeamCoaches { get; set; } = [];
    public List<TeamPlayer> TeamPlayers { get; set; } = [];
    public List<TeamTrainingGroup> TrainingGroups { get; set; } = [];
    public List<TeamMatch> Matches { get; set; } = [];
    public List<TeamTournament> Tournaments { get; set; } = [];
    public List<TeamMessage> Messages { get; set; } = [];
    public List<TeamInjury> Injuries { get; set; } = [];
    public List<TeamScheduleEvent> ScheduleEvents { get; set; } = [];
}

public sealed class TeamTrainingGroup
{
    public int Id { get; set; }
    public int TeamId { get; set; }
    public Team Team { get; set; } = null!;
    public required string Name { get; set; }
    public string? Purpose { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<TeamTrainingGroupPlayer> Players { get; set; } = [];
}

public sealed class TeamTrainingGroupPlayer
{
    public int TeamTrainingGroupId { get; set; }
    public TeamTrainingGroup TeamTrainingGroup { get; set; } = null!;
    public int PlayerId { get; set; }
    public PlayerProfile Player { get; set; } = null!;
}

public sealed class TeamMatch
{
    public int Id { get; set; }
    public int TeamId { get; set; }
    public Team Team { get; set; } = null!;
    public required string Opponent { get; set; }
    public string? Competition { get; set; }
    public DateTimeOffset ScheduledAt { get; set; }
    public string Venue { get; set; } = "Дома";
    public string Status { get; set; } = "Запланирован";
    public int? GoalsFor { get; set; }
    public int? GoalsAgainst { get; set; }
    public string? LineupNotes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class TeamTournament
{
    public int Id { get; set; }
    public int TeamId { get; set; }
    public Team Team { get; set; } = null!;
    public required string Name { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string Status { get; set; } = "Запланирован";
    public string? Placement { get; set; }
    public decimal EntryFee { get; set; }
    public decimal TravelCost { get; set; }
    public decimal AccommodationCost { get; set; }
    public decimal MealCost { get; set; }
    public decimal EquipmentCost { get; set; }
    public decimal OtherCost { get; set; }
    public decimal Income { get; set; }
    public string? SourceUrl { get; set; }
    public DateOnly? RegistrationDeadline { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class TeamCoach
{
    public int TeamId { get; set; }
    public Team Team { get; set; } = null!;
    public int CoachId { get; set; }
    public CoachProfile Coach { get; set; } = null!;
    public bool IsHeadCoach { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class TeamPlayer
{
    public int TeamId { get; set; }
    public Team Team { get; set; } = null!;
    public int PlayerId { get; set; }
    public PlayerProfile Player { get; set; } = null!;
    public int? ShirtNumber { get; set; }
    public string TournamentRegistrationStatus { get; set; } = "Не заявлен";
    public string CurrentSeasonPlan { get; set; } = "Основной состав";
    public string NextSeasonPlan { get; set; } = "Оценить развитие";
    public string TwoYearPlan { get; set; } = "Перспектива";
    public bool IsActive { get; set; } = true;
    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LeftAt { get; set; }
}

public sealed class TeamMessage
{
    public int Id { get; set; }
    public int TeamId { get; set; }
    public Team Team { get; set; } = null!;
    public required string AuthorUserId { get; set; }
    public ApplicationUser AuthorUser { get; set; } = null!;
    public TeamMessageChannel Channel { get; set; }
    public required string Text { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class TeamInjury
{
    public int Id { get; set; }
    public int TeamId { get; set; }
    public Team Team { get; set; } = null!;
    public int PlayerId { get; set; }
    public PlayerProfile Player { get; set; } = null!;
    public required string Type { get; set; }
    public string Severity { get; set; } = "Незначительная";
    public string Status { get; set; } = "Лечение";
    public int RiskLevel { get; set; }
    public DateOnly StartedOn { get; set; }
    public DateOnly? ExpectedReturnOn { get; set; }
    public DateOnly? ClosedOn { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class TeamScheduleEvent
{
    public int Id { get; set; }
    public int TeamId { get; set; }
    public Team Team { get; set; } = null!;
    public required string Type { get; set; }
    public required string Title { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset? ReminderAt { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class TeamTraining
{
    public int Id { get; set; }
    public int TeamId { get; set; }
    public Team Team { get; set; } = null!;
    public int CoachId { get; set; }
    public CoachProfile Coach { get; set; } = null!;
    public required string Title { get; set; }
    public DateTimeOffset ScheduledAt { get; set; }
    public TeamTrainingStatus Status { get; set; } = TeamTrainingStatus.Planned;
    public string? Notes { get; set; }
    public DateTimeOffset? AttendanceSavedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<TeamTrainingExercise> Exercises { get; set; } = [];
    public List<TeamTrainingAttendance> Attendances { get; set; } = [];
}

public sealed class TeamTrainingExercise
{
    public int Id { get; set; }
    public int TeamTrainingId { get; set; }
    public TeamTraining TeamTraining { get; set; } = null!;
    public int ExerciseId { get; set; }
    public Exercise Exercise { get; set; } = null!;
    public int SortOrder { get; set; }
    public List<TeamTrainingPlayerResult> PlayerResults { get; set; } = [];
}

public sealed class TeamTrainingAttendance
{
    public int TeamTrainingId { get; set; }
    public TeamTraining TeamTraining { get; set; } = null!;
    public int PlayerId { get; set; }
    public PlayerProfile Player { get; set; } = null!;
    public AttendanceStatus Status { get; set; } = AttendanceStatus.Unknown;
}

public sealed class TeamTrainingPlayerResult
{
    public int TeamTrainingExerciseId { get; set; }
    public TeamTrainingExercise TeamTrainingExercise { get; set; } = null!;
    public int PlayerId { get; set; }
    public PlayerProfile Player { get; set; } = null!;
    public bool IsCompleted { get; set; }
    public bool Understood { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class PlayerProfile
{
    public int Id { get; set; }
    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public int? MunicipalityId { get; set; }
    public Municipality? Municipality { get; set; }
    public required string PreferredPosition { get; set; }
    public required string DominantFoot { get; set; }
    public required string ExperienceLevel { get; set; }
    public decimal? Height { get; set; }
    public decimal? Weight { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ParentProfile
{
    public int Id { get; set; }
    public required string UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
}

public sealed class ParentPlayerLink
{
    public int ParentId { get; set; }
    public ParentProfile Parent { get; set; } = null!;
    public int PlayerId { get; set; }
    public PlayerProfile Player { get; set; } = null!;
    public required string Relationship { get; set; }
    public bool IsPrimary { get; set; }
    public bool ConsentAccepted { get; set; }
    public required string ConsentVersion { get; set; }
    public DateTimeOffset? ConsentAcceptedAt { get; set; }
}

public sealed class CoachProfile
{
    public int Id { get; set; }
    public required string UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public required string DisplayName { get; set; }
}

public sealed class PublicOrganizerProfile
{
    public int Id { get; set; }
    public required string UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public required string DisplayName { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public int MunicipalityId { get; set; }
    public Municipality Municipality { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class CoachPlayerLink
{
    public int CoachId { get; set; }
    public CoachProfile Coach { get; set; } = null!;
    public int PlayerId { get; set; }
    public PlayerProfile Player { get; set; } = null!;
    public LinkStatus Status { get; set; } = LinkStatus.Active;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class AssessmentDefinition
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required string Instructions { get; set; }
    public required string Unit { get; set; }
    public SkillCategory SkillCategory { get; set; }
    public ScoringDirection ScoringDirection { get; set; }
    public decimal MinimumReasonableValue { get; set; }
    public decimal MaximumReasonableValue { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class AssessmentNorm
{
    public int Id { get; set; }
    public int AssessmentDefinitionId { get; set; }
    public AssessmentDefinition AssessmentDefinition { get; set; } = null!;
    public int MinimumAge { get; set; }
    public int MaximumAge { get; set; }
    public decimal LowPerformanceValue { get; set; }
    public decimal HighPerformanceValue { get; set; }
    public bool IsDemo { get; set; } = true;
    public required string SourceNote { get; set; }
}

public sealed class AssessmentSession
{
    public int Id { get; set; }
    public int PlayerId { get; set; }
    public PlayerProfile Player { get; set; } = null!;
    public bool IsCompleted { get; set; }
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public List<AssessmentResult> Results { get; set; } = [];
}

public sealed class AssessmentResult
{
    public int Id { get; set; }
    public int AssessmentSessionId { get; set; }
    public AssessmentSession AssessmentSession { get; set; } = null!;
    public int AssessmentDefinitionId { get; set; }
    public AssessmentDefinition AssessmentDefinition { get; set; } = null!;
    public decimal RawValue { get; set; }
    public int NormalizedScore { get; set; }
}

public sealed class SkillSnapshot
{
    public int Id { get; set; }
    public int PlayerId { get; set; }
    public PlayerProfile Player { get; set; } = null!;
    public int AssessmentSessionId { get; set; }
    public int Speed { get; set; }
    public int Endurance { get; set; }
    public int BallControl { get; set; }
    public int Passing { get; set; }
    public int Shooting { get; set; }
    public int Agility { get; set; }
    public DateTimeOffset CapturedAt { get; set; } = DateTimeOffset.UtcNow;

    public int Get(SkillCategory category) => category switch
    {
        SkillCategory.Speed => Speed,
        SkillCategory.Endurance => Endurance,
        SkillCategory.BallControl => BallControl,
        SkillCategory.Passing => Passing,
        SkillCategory.Shooting => Shooting,
        _ => Agility
    };
}

public sealed class Exercise
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required string Instructions { get; set; }
    public SkillCategory SkillCategory { get; set; }
    public int Difficulty { get; set; }
    public int DurationMinutes { get; set; }
    public required string Equipment { get; set; }
    public string? VideoUrl { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class TrainingProgram
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public int Weeks { get; set; } = 4;
    public bool IsActive { get; set; } = true;
}

public sealed class TrainingProgramExercise
{
    public int TrainingProgramId { get; set; }
    public TrainingProgram TrainingProgram { get; set; } = null!;
    public int ExerciseId { get; set; }
    public Exercise Exercise { get; set; } = null!;
    public int SortOrder { get; set; }
}

public sealed class TrainingPlan
{
    public int Id { get; set; }
    public int PlayerId { get; set; }
    public PlayerProfile Player { get; set; } = null!;
    public int? TrainingProgramId { get; set; }
    public TrainingProgram? TrainingProgram { get; set; }
    public DateOnly WeekStart { get; set; }
    public PlanStatus Status { get; set; } = PlanStatus.Active;
    public string GenerationReason { get; set; } = "Персональный план";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<TrainingDay> Days { get; set; } = [];
}

public sealed class TrainingDay
{
    public int Id { get; set; }
    public int TrainingPlanId { get; set; }
    public TrainingPlan TrainingPlan { get; set; } = null!;
    public DateOnly PlannedDate { get; set; }
    public string Title { get; set; } = "Тренировка";
    public List<TrainingExercise> Exercises { get; set; } = [];
}

public sealed class TrainingExercise
{
    public int Id { get; set; }
    public int TrainingDayId { get; set; }
    public TrainingDay TrainingDay { get; set; } = null!;
    public int ExerciseId { get; set; }
    public Exercise Exercise { get; set; } = null!;
    public int SortOrder { get; set; }
    public int TargetDurationMinutes { get; set; }
    public int? TargetRepetitions { get; set; }
}

public sealed class TrainingSession
{
    public int Id { get; set; }
    public int PlayerId { get; set; }
    public PlayerProfile Player { get; set; } = null!;
    public int TrainingDayId { get; set; }
    public TrainingDay TrainingDay { get; set; } = null!;
    public SessionStatus Status { get; set; } = SessionStatus.Planned;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? Notes { get; set; }
    public List<TrainingExerciseResult> Results { get; set; } = [];
}

public sealed class TrainingExerciseResult
{
    public int Id { get; set; }
    public int TrainingSessionId { get; set; }
    public TrainingSession TrainingSession { get; set; } = null!;
    public int TrainingExerciseId { get; set; }
    public TrainingExercise TrainingExercise { get; set; } = null!;
    public bool IsCompleted { get; set; }
    public int? DurationMinutes { get; set; }
    public int? Repetitions { get; set; }
    public string? Notes { get; set; }
    public int? PerceivedDifficulty { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class AchievementDefinition
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
}

public sealed class PlayerAchievement
{
    public int Id { get; set; }
    public int PlayerId { get; set; }
    public int AchievementDefinitionId { get; set; }
    public AchievementDefinition AchievementDefinition { get; set; } = null!;
    public DateTimeOffset AwardedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Sport
{
    public int Id { get; set; }
    public required string Slug { get; set; }
    public required string Name { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class SportsVenue
{
    public int Id { get; set; }
    public required string Slug { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string Country { get; set; } = "Россия";
    public required string Region { get; set; }
    public required string City { get; set; }
    public string? District { get; set; }
    public required string Address { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public bool Indoor { get; set; }
    public string? SurfaceType { get; set; }
    public bool HasChangingRooms { get; set; }
    public bool HasLighting { get; set; }
    public bool HasParking { get; set; }
    public string? ContactPhone { get; set; }
    public string? Website { get; set; }
    public bool IsVerified { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class PublicActivity
{
    public int Id { get; set; }
    public required string Slug { get; set; }
    public int SportId { get; set; }
    public Sport Sport { get; set; } = null!;
    public PublicActivityType EventType { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public required string OrganizerId { get; set; }
    public ApplicationUser Organizer { get; set; } = null!;
    public int SportsVenueId { get; set; }
    public SportsVenue Venue { get; set; } = null!;
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }
    public string TimeZone { get; set; } = "Europe/Moscow";
    public bool IsRecurring { get; set; }
    public string? RecurrenceRule { get; set; }
    public int Capacity { get; set; }
    public int WaitlistCapacity { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = "RUB";
    public string SkillLevel { get; set; } = "Любой";
    public int MinimumAge { get; set; } = 18;
    public int? MaximumAge { get; set; }
    public string GenderPolicy { get; set; } = "Любой";
    public string? EquipmentRequirements { get; set; }
    public string? Rules { get; set; }
    public string? CancellationPolicy { get; set; }
    public PublicActivityStatus Status { get; set; } = PublicActivityStatus.Draft;
    public PublicActivityVisibility Visibility { get; set; } = PublicActivityVisibility.Public;
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset? RegistrationDeadline { get; set; }
    public int Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<PublicActivityParticipant> Participants { get; set; } = [];
}

public sealed class PublicActivityParticipant
{
    public long Id { get; set; }
    public int PublicActivityId { get; set; }
    public PublicActivity Activity { get; set; } = null!;
    public required string UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public PublicParticipantStatus Status { get; set; } = PublicParticipantStatus.Pending;
    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ConfirmedAt { get; set; }
    public DateTimeOffset? CheckedInAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public string Source { get; set; } = "web";
}

public sealed class CoachNote
{
    public int Id { get; set; }
    public int CoachId { get; set; }
    public int PlayerId { get; set; }
    public required string Text { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class AuditLog
{
    public long Id { get; set; }
    public string? UserId { get; set; }
    public required string EventType { get; set; }
    public required string EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? Details { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
