namespace Kasanie.Api.Contracts;

public sealed record GoalkeeperAppointmentRequest(int CoachId);
public sealed record GoalkeeperExerciseRequest(string Title, string Category, string Description, string Equipment, int DurationMinutes, bool IsActive = true);
public sealed record GoalkeeperPlanStep(int? ExerciseId, string Title, string Stage, int DurationMinutes, string Instructions);
public sealed record GoalkeeperParticipant(int PlayerId, string Attendance, int? Rating, string Notes);
public sealed record GoalkeeperTrainingRequest(string Title, DateTimeOffset ScheduledAt, string Venue, string Objective, string Intensity, List<GoalkeeperPlanStep> Plan, List<GoalkeeperParticipant> Players, string Notes, int Version = 0, string Status = "Planned");
public sealed record GoalkeeperRecordRequest(string Goal, string Notes, int Version = 0);
public sealed record GoalkeeperCreatePlayerRequest(string FirstName, string LastName, DateOnly DateOfBirth, string DominantFoot, int? ShirtNumber = null);
