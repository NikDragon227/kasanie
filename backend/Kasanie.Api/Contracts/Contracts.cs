using System.ComponentModel.DataAnnotations;
using Kasanie.Api.Domain;

namespace Kasanie.Api.Contracts;

public sealed record RegisterRequest(string Email, string Password, DateOnly DateOfBirth, string FirstName, string LastName);
public sealed record RegisterOrganizerRequest(string Email, string Password, DateOnly DateOfBirth, string DisplayName, string City);
public sealed record RegisterPortalUserRequest(string Email, string Password, DateOnly DateOfBirth, string DisplayName, string Role);
public sealed record LoginRequest(string Email, string Password);
public sealed record EmailRequest(string Email);
public sealed record ConfirmEmailRequest(string UserId, string Token);
public sealed record ResetPasswordRequest(string Email, string Token, string NewPassword);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public sealed record UserDto(string Id, string Email, string[] Roles);
public sealed record ProfileUpdateRequest(string FirstName, string LastName, string? Gender, string City, string PreferredPosition, string DominantFoot, string ExperienceLevel, decimal? Height, decimal? Weight);
public sealed record AssessmentValueRequest(int DefinitionId, decimal Value);
public sealed record SubmitAssessmentRequest(List<AssessmentValueRequest> Values);
public sealed record ExerciseResultRequest(bool IsCompleted, int? DurationMinutes, int? Repetitions, string? Notes, int? PerceivedDifficulty);
public sealed record CompleteWorkoutRequest(string? Notes);
public sealed record ChildCreateRequest(string FirstName, string LastName, DateOnly DateOfBirth, string City, string PreferredPosition, string DominantFoot, string ExperienceLevel, string Relationship, bool ConsentAccepted, string ConsentVersion);
public sealed record ConsentRequest(bool Accepted, string Version);
public sealed record CoachNoteRequest(string Text);
public sealed record AddPlanExerciseRequest(int TrainingDayId, int ExerciseId);
public sealed record ReplacePlanExerciseRequest(int TrainingExerciseId, int ExerciseId);
public sealed record AssignProgramRequest(int TrainingProgramId);
public sealed record ExerciseUpsertRequest(string Name, string Description, string Instructions, SkillCategory SkillCategory, int Difficulty, int DurationMinutes, string Equipment, string? VideoUrl, string? ImageUrl, bool IsActive);
public sealed record MunicipalityRequest(string Name, string Region, bool IsActive);
public sealed record AnalystRegionRequest(string Region);
public sealed record InviteUserRequest(string Email, string Role, string? Region);
public sealed record UserLockRequest(bool Locked);
public sealed record TrainingProgramUpsertRequest(string Name, string Description, int Weeks, bool IsActive);
public sealed record AssessmentNormRequest(int MinimumAge, int MaximumAge, decimal LowPerformanceValue, decimal HighPerformanceValue, bool IsDemo, string SourceNote);
public sealed record AssessmentUpsertRequest(string Name, string Description, string Instructions, string Unit, SkillCategory SkillCategory, ScoringDirection ScoringDirection, decimal MinimumReasonableValue, decimal MaximumReasonableValue, int SortOrder, bool IsActive, List<AssessmentNormRequest> Norms);
public sealed record SchoolCreateRequest(string Name, string OwnerEmail, string? City, string? ContactEmail, string? Phone);
public sealed record SchoolUpdateRequest(string Name, string? City, string? ContactEmail, string? Phone, string? LogoUrl);
public sealed record SchoolStatusRequest(bool IsActive);
public sealed record TeamUpsertRequest(string Name, string? AgeGroup, string? Season, string? TrainingCycleStage = null, DateOnly? CycleStart = null, DateOnly? CycleEnd = null, int? HeadCoachId = null, bool IsActive = true);
public sealed record TeamIdentityUpdateRequest(string Name, string AgeGroup, string? Season, int? HeadCoachId, bool IsActive = true);
public sealed record SchoolCoachInviteRequest(string Email, string DisplayName);
public sealed record TeamCoachRequest(int CoachId, bool IsHeadCoach);
public sealed record SchoolPlayerCreateRequest(string FirstName, string LastName, DateOnly DateOfBirth, string City, string PreferredPosition, string DominantFoot, string ExperienceLevel, int TeamId, int? ShirtNumber);
public sealed record TeamPlayerRequest(int PlayerId, int? ShirtNumber);
public sealed record TeamTrainingGroupRequest(string Name, string? Purpose, List<int> PlayerIds);
public sealed record TeamTacticRequest(string? Formation, string? Notes, string? PlanJson = null, string? SetPiecesJson = null, string? OpponentInstructions = null);
public sealed record TeamCycleRequest(string Stage, DateOnly? StartsOn, DateOnly? EndsOn);
public sealed record TeamPlayerManagementRequest(int? ShirtNumber, string TournamentRegistrationStatus, string CurrentSeasonPlan, string NextSeasonPlan, string TwoYearPlan);
public sealed record TeamMessageRequest(string Channel, string Text);
public sealed record TeamCollectiveRequest(string? CodeOfConduct);
public sealed record TeamOpponentReportRequest(string? SourceUrl, string? Notes);
public sealed record TeamInjuryRequest(int PlayerId, string Type, string Severity, string Status, int RiskLevel, DateOnly StartedOn, DateOnly? ExpectedReturnOn, string? Notes);
public sealed record TeamScheduleEventRequest(string Type, string Title, DateTimeOffset StartsAt, DateTimeOffset? ReminderAt, string? Notes);
public sealed record TeamMatchRequest(string Opponent, string? Competition, DateTimeOffset ScheduledAt, string Venue, string Status = "Запланирован", int? GoalsFor = null, int? GoalsAgainst = null, string? LineupNotes = null);
public sealed record TeamTournamentRequest(string Name, DateOnly StartDate, DateOnly? EndDate, string Status, string? Placement, decimal EntryFee, decimal TravelCost, decimal AccommodationCost, decimal MealCost, decimal EquipmentCost, decimal OtherCost, decimal Income, string? SourceUrl = null, DateOnly? RegistrationDeadline = null);
public sealed record CreateTeamTrainingRequest(int TeamId, string Title, DateTimeOffset ScheduledAt, List<int> ExerciseIds);
public sealed record TeamAttendanceItemRequest(int PlayerId, string Status);
public sealed record SaveTeamAttendanceRequest(List<TeamAttendanceItemRequest> Players);
public sealed record TeamTrainingResultRequest(int PlayerId, int TeamTrainingExerciseId, bool IsCompleted, bool Understood);
public sealed record SaveTeamTrainingReviewRequest(List<TeamTrainingResultRequest> Results, string? Notes);

public static class Validation
{
    public static Dictionary<string, string[]> Register(RegisterRequest value)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(value.Email) || !value.Email.Contains('@')) errors["email"] = ["Укажите корректный email."];
        if (string.IsNullOrEmpty(value.Password) || value.Password.Length < 8) errors["password"] = ["Пароль должен содержать не менее 8 символов."];
        if (string.IsNullOrWhiteSpace(value.FirstName)) errors["firstName"] = ["Укажите имя."];
        if (string.IsNullOrWhiteSpace(value.LastName)) errors["lastName"] = ["Укажите фамилию."];
        return errors;
    }

    public static Dictionary<string, string[]> RegisterOrganizer(RegisterOrganizerRequest value)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(value.Email) || !new EmailAddressAttribute().IsValid(value.Email)) errors["email"] = ["Укажите корректный email."];
        if (string.IsNullOrEmpty(value.Password) || value.Password.Length < 8) errors["password"] = ["Пароль должен содержать не менее 8 символов."];
        if (string.IsNullOrWhiteSpace(value.DisplayName)) errors["displayName"] = ["Укажите имя организатора."];
        else if (value.DisplayName.Trim().Length > 120) errors["displayName"] = ["Имя организатора не должно превышать 120 символов."];
        if (string.IsNullOrWhiteSpace(value.City)) errors["city"] = ["Укажите город."];
        return errors;
    }

    public static Dictionary<string, string[]> RegisterPortalUser(RegisterPortalUserRequest value)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(value.Email) || !new EmailAddressAttribute().IsValid(value.Email)) errors["email"] = ["Укажите корректный email."];
        if (string.IsNullOrEmpty(value.Password) || value.Password.Length < 8) errors["password"] = ["Пароль должен содержать не менее 8 символов."];
        if (string.IsNullOrWhiteSpace(value.DisplayName)) errors["displayName"] = ["Укажите имя и фамилию."];
        else if (value.DisplayName.Trim().Length > 120) errors["displayName"] = ["Имя не должно превышать 120 символов."];
        if (value.Role is not (Roles.Parent or Roles.Coach)) errors["role"] = ["Для самостоятельной регистрации доступны роли родителя и тренера."];
        return errors;
    }

    public static Dictionary<string, string[]> Exercise(ExerciseUpsertRequest value)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(value.Name)) errors["name"] = ["Название обязательно."];
        if (value.Difficulty is < 1 or > 5) errors["difficulty"] = ["Сложность должна быть от 1 до 5."];
        if (value.DurationMinutes is < 1 or > 180) errors["durationMinutes"] = ["Длительность должна быть от 1 до 180 минут."];
        return errors;
    }

    public static Dictionary<string, string[]> Municipality(MunicipalityRequest value)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(value.Name)) errors["name"] = ["Название города обязательно."];
        if (string.IsNullOrWhiteSpace(value.Region)) errors["region"] = ["Регион обязателен."];
        return errors;
    }

    public static Dictionary<string, string[]> Program(TrainingProgramUpsertRequest value)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(value.Name)) errors["name"] = ["Название обязательно."];
        if (value.Weeks is < 1 or > 52) errors["weeks"] = ["Длительность должна быть от 1 до 52 недель."];
        return errors;
    }

    public static Dictionary<string, string[]> Assessment(AssessmentUpsertRequest value)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(value.Name) || string.IsNullOrWhiteSpace(value.Description) || string.IsNullOrWhiteSpace(value.Instructions) || string.IsNullOrWhiteSpace(value.Unit)) errors["assessment"] = ["Заполните название, описание, инструкцию и единицу измерения."];
        if (value.MinimumReasonableValue >= value.MaximumReasonableValue) errors["range"] = ["Минимальное значение должно быть меньше максимального."];
        if (value.Norms.Count == 0 || value.Norms.Any(x => x.MinimumAge < 3 || x.MaximumAge > 25 || x.MinimumAge > x.MaximumAge || x.LowPerformanceValue == x.HighPerformanceValue || string.IsNullOrWhiteSpace(x.SourceNote))) errors["norms"] = ["Проверьте возрастной диапазон, пороги и источник нормы."];
        return errors;
    }
}
