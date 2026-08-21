using Kasanie.Api.Domain;

namespace Kasanie.Api.Contracts;

public sealed record RegisterRequest(string Email, string Password, DateOnly DateOfBirth, string FirstName, string LastName, string City, string PreferredPosition, string DominantFoot, string ExperienceLevel);
public sealed record LoginRequest(string Email, string Password);
public sealed record EmailRequest(string Email);
public sealed record ConfirmEmailRequest(string UserId, string Token);
public sealed record ResetPasswordRequest(string Email, string Token, string NewPassword);
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

public static class Validation
{
    public static Dictionary<string, string[]> Register(RegisterRequest value)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(value.Email) || !value.Email.Contains('@')) errors["email"] = ["Укажите корректный email."];
        if (value.Password.Length < 10) errors["password"] = ["Пароль должен содержать не менее 10 символов."];
        if (string.IsNullOrWhiteSpace(value.FirstName)) errors["firstName"] = ["Укажите имя."];
        if (string.IsNullOrWhiteSpace(value.LastName)) errors["lastName"] = ["Укажите фамилию."];
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
