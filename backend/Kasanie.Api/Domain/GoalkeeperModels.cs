namespace Kasanie.Api.Domain;

// A specialist appointment is intentionally separate from TeamCoach: it must not
// grant permissions to change the whole squad, tactics or the main training plan.
public sealed class GoalkeeperCoachAssignment
{
    public int TeamId { get; set; }
    public Team Team { get; set; } = null!;
    public int CoachId { get; set; }
    public CoachProfile Coach { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class GoalkeeperExercise
{
    public int Id { get; set; }
    public int TeamId { get; set; }
    public Team Team { get; set; } = null!;
    public int CoachId { get; set; }
    public CoachProfile Coach { get; set; } = null!;
    public required string Title { get; set; }
    public string Category { get; set; } = "Техника";
    public string Description { get; set; } = "";
    public string Equipment { get; set; } = "";
    public int DurationMinutes { get; set; } = 10;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class GoalkeeperTraining
{
    public int Id { get; set; }
    public int TeamId { get; set; }
    public Team Team { get; set; } = null!;
    public int CoachId { get; set; }
    public CoachProfile Coach { get; set; } = null!;
    public required string Title { get; set; }
    public DateTimeOffset ScheduledAt { get; set; }
    public int DurationMinutes { get; set; }
    public string Status { get; set; } = "Planned";
    public string StateJson { get; set; } = "{}";
    public int Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class GoalkeeperPlayerRecord
{
    public int TeamId { get; set; }
    public int CoachId { get; set; }
    public int PlayerId { get; set; }
    public Team Team { get; set; } = null!;
    public CoachProfile Coach { get; set; } = null!;
    public PlayerProfile Player { get; set; } = null!;
    public string Goal { get; set; } = "";
    public string Notes { get; set; } = "";
    public int Version { get; set; } = 1;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
