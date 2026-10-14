namespace Kasanie.Api.Contracts;

public sealed record TrainingCalendarEntry(DateOnly Date, int Slot, string Session, string Category, int Intensity, int Minutes, string Group, string? Notes);
public sealed record TrainingTactic(string Name, string Formation, string? Notes);
public sealed record TrainingMentoringGroup(string Id, string Name, int? MentorId, List<int> PlayerIds);
public sealed record TrainingPlayerSettings(int PlayerId, string Group, string Position, string Role, string Focus, string Intensity, bool WeakFoot, DateOnly? RestUntil, string? Notes);
public sealed record TrainingCoachTasks(int CoachId, List<string> Tasks);

public sealed class TrainingWorkspaceState
{
    public int? ResponsibleCoachId { get; set; }
    public List<TrainingTactic> Tactics { get; set; } = [];
    public List<TrainingCalendarEntry> Calendar { get; set; } = [];
    public List<TrainingPlayerSettings> Players { get; set; } = [];
    public List<TrainingMentoringGroup> Mentoring { get; set; } = [];
    public List<TrainingCoachTasks> Coaches { get; set; } = [];
    public string? Satisfaction { get; set; }
    public string? MatchPreparation { get; set; }
    public string? LastMatchComment { get; set; }
}

public sealed record SaveTrainingWorkspaceRequest(int Version, int UtcOffsetMinutes, TrainingWorkspaceState? State);
