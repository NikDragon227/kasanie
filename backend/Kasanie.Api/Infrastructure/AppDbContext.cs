using Kasanie.Api.Domain;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Kasanie.Api.Infrastructure;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Municipality> Municipalities => Set<Municipality>();
    public DbSet<School> Schools => Set<School>();
    public DbSet<SchoolMembership> SchoolMemberships => Set<SchoolMembership>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamCoach> TeamCoaches => Set<TeamCoach>();
    public DbSet<TeamPlayer> TeamPlayers => Set<TeamPlayer>();
    public DbSet<TeamTrainingGroup> TeamTrainingGroups => Set<TeamTrainingGroup>();
    public DbSet<TeamTrainingGroupPlayer> TeamTrainingGroupPlayers => Set<TeamTrainingGroupPlayer>();
    public DbSet<TeamMatch> TeamMatches => Set<TeamMatch>();
    public DbSet<TeamTournament> TeamTournaments => Set<TeamTournament>();
    public DbSet<TeamMessage> TeamMessages => Set<TeamMessage>();
    public DbSet<TeamInjury> TeamInjuries => Set<TeamInjury>();
    public DbSet<TeamScheduleEvent> TeamScheduleEvents => Set<TeamScheduleEvent>();
    public DbSet<TeamTraining> TeamTrainings => Set<TeamTraining>();
    public DbSet<TeamTrainingExercise> TeamTrainingExercises => Set<TeamTrainingExercise>();
    public DbSet<TeamTrainingAttendance> TeamTrainingAttendances => Set<TeamTrainingAttendance>();
    public DbSet<TeamTrainingPlayerResult> TeamTrainingPlayerResults => Set<TeamTrainingPlayerResult>();
    public DbSet<PlayerProfile> Players => Set<PlayerProfile>();
    public DbSet<ParentProfile> ParentProfiles => Set<ParentProfile>();
    public DbSet<ParentPlayerLink> ParentPlayerLinks => Set<ParentPlayerLink>();
    public DbSet<CoachProfile> CoachProfiles => Set<CoachProfile>();
    public DbSet<PublicOrganizerProfile> PublicOrganizerProfiles => Set<PublicOrganizerProfile>();
    public DbSet<CoachPlayerLink> CoachPlayerLinks => Set<CoachPlayerLink>();
    public DbSet<AssessmentDefinition> AssessmentDefinitions => Set<AssessmentDefinition>();
    public DbSet<AssessmentNorm> AssessmentNorms => Set<AssessmentNorm>();
    public DbSet<AssessmentSession> AssessmentSessions => Set<AssessmentSession>();
    public DbSet<AssessmentResult> AssessmentResults => Set<AssessmentResult>();
    public DbSet<SkillSnapshot> SkillSnapshots => Set<SkillSnapshot>();
    public DbSet<Exercise> Exercises => Set<Exercise>();
    public DbSet<TrainingProgram> TrainingPrograms => Set<TrainingProgram>();
    public DbSet<TrainingProgramExercise> TrainingProgramExercises => Set<TrainingProgramExercise>();
    public DbSet<TrainingPlan> TrainingPlans => Set<TrainingPlan>();
    public DbSet<TrainingDay> TrainingDays => Set<TrainingDay>();
    public DbSet<TrainingExercise> TrainingExercises => Set<TrainingExercise>();
    public DbSet<TrainingSession> TrainingSessions => Set<TrainingSession>();
    public DbSet<TrainingExerciseResult> TrainingExerciseResults => Set<TrainingExerciseResult>();
    public DbSet<AchievementDefinition> AchievementDefinitions => Set<AchievementDefinition>();
    public DbSet<PlayerAchievement> PlayerAchievements => Set<PlayerAchievement>();
    public DbSet<Sport> Sports => Set<Sport>();
    public DbSet<SportsVenue> SportsVenues => Set<SportsVenue>();
    public DbSet<PublicActivity> PublicActivities => Set<PublicActivity>();
    public DbSet<PublicActivityParticipant> PublicActivityParticipants => Set<PublicActivityParticipant>();
    public DbSet<CoachNote> CoachNotes => Set<CoachNote>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasPostgresEnum<SkillCategory>();

        builder.Entity<PlayerProfile>().HasIndex(x => x.UserId).IsUnique();
        builder.Entity<ParentProfile>().HasIndex(x => x.UserId).IsUnique();
        builder.Entity<CoachProfile>().HasIndex(x => x.UserId).IsUnique();
        builder.Entity<PublicOrganizerProfile>().HasIndex(x => x.UserId).IsUnique();
        builder.Entity<School>().HasIndex(x => x.Slug).IsUnique();
        builder.Entity<SchoolMembership>().HasKey(x => new { x.SchoolId, x.UserId });
        builder.Entity<TeamCoach>().HasKey(x => new { x.TeamId, x.CoachId });
        builder.Entity<TeamPlayer>().HasKey(x => new { x.TeamId, x.PlayerId });
        builder.Entity<TeamTrainingGroupPlayer>().HasKey(x => new { x.TeamTrainingGroupId, x.PlayerId });
        builder.Entity<TeamTrainingAttendance>().HasKey(x => new { x.TeamTrainingId, x.PlayerId });
        builder.Entity<TeamTrainingPlayerResult>().HasKey(x => new { x.TeamTrainingExerciseId, x.PlayerId });
        builder.Entity<TeamTrainingExercise>().HasIndex(x => new { x.TeamTrainingId, x.ExerciseId }).IsUnique();
        builder.Entity<TeamPlayer>().HasIndex(x => new { x.TeamId, x.ShirtNumber }).IsUnique().HasFilter("\"IsActive\" AND \"ShirtNumber\" IS NOT NULL");
        builder.Entity<TeamMessage>().HasIndex(x => new { x.TeamId, x.Channel, x.CreatedAt });
        builder.Entity<TeamInjury>().HasIndex(x => new { x.TeamId, x.Status });
        builder.Entity<TeamScheduleEvent>().HasIndex(x => new { x.TeamId, x.StartsAt });
        builder.Entity<ParentPlayerLink>().HasKey(x => new { x.ParentId, x.PlayerId });
        builder.Entity<CoachPlayerLink>().HasKey(x => new { x.CoachId, x.PlayerId });
        builder.Entity<TrainingProgramExercise>().HasKey(x => new { x.TrainingProgramId, x.ExerciseId });
        builder.Entity<AssessmentResult>().HasIndex(x => new { x.AssessmentSessionId, x.AssessmentDefinitionId }).IsUnique();
        builder.Entity<TrainingSession>().HasIndex(x => new { x.PlayerId, x.TrainingDayId }).IsUnique();
        builder.Entity<TrainingExerciseResult>().HasIndex(x => new { x.TrainingSessionId, x.TrainingExerciseId }).IsUnique();
        builder.Entity<AchievementDefinition>().HasIndex(x => x.Code).IsUnique();
        builder.Entity<Sport>().HasIndex(x => x.Slug).IsUnique();
        builder.Entity<SportsVenue>().HasIndex(x => x.Slug).IsUnique();
        builder.Entity<SportsVenue>().HasIndex(x => new { x.City, x.District });
        builder.Entity<PublicActivity>().HasIndex(x => x.Slug).IsUnique();
        builder.Entity<PublicActivity>().HasIndex(x => new { x.Status, x.Visibility, x.StartAt });
        builder.Entity<PublicActivity>().HasIndex(x => new { x.SportId, x.SportsVenueId, x.StartAt });
        builder.Entity<PublicActivity>().Property(x => x.Price).HasPrecision(12, 2);
        builder.Entity<PublicActivity>().Property(x => x.Version).IsConcurrencyToken();
        builder.Entity<PublicActivityParticipant>().HasIndex(x => new { x.PublicActivityId, x.UserId }).IsUnique();
        builder.Entity<PublicActivityParticipant>().HasIndex(x => new { x.PublicActivityId, x.Status, x.JoinedAt });

        builder.Entity<PlayerProfile>().Property(x => x.Height).HasPrecision(5, 1);
        builder.Entity<PlayerProfile>().Property(x => x.Weight).HasPrecision(5, 1);
        builder.Entity<AssessmentDefinition>().Property(x => x.MinimumReasonableValue).HasPrecision(10, 2);
        builder.Entity<AssessmentDefinition>().Property(x => x.MaximumReasonableValue).HasPrecision(10, 2);
        builder.Entity<AssessmentNorm>().Property(x => x.LowPerformanceValue).HasPrecision(10, 2);
        builder.Entity<AssessmentNorm>().Property(x => x.HighPerformanceValue).HasPrecision(10, 2);
        builder.Entity<TeamTournament>().Property(x => x.EntryFee).HasPrecision(12, 2);
        builder.Entity<TeamTournament>().Property(x => x.TravelCost).HasPrecision(12, 2);
        builder.Entity<TeamTournament>().Property(x => x.AccommodationCost).HasPrecision(12, 2);
        builder.Entity<TeamTournament>().Property(x => x.MealCost).HasPrecision(12, 2);
        builder.Entity<TeamTournament>().Property(x => x.EquipmentCost).HasPrecision(12, 2);
        builder.Entity<TeamTournament>().Property(x => x.OtherCost).HasPrecision(12, 2);
        builder.Entity<TeamTournament>().Property(x => x.Income).HasPrecision(12, 2);
        builder.Entity<Team>().Property(x => x.TrainingCycleStage).HasDefaultValue("Подготовительный этап");

        foreach (var relationship in builder.Model.GetEntityTypes().SelectMany(x => x.GetForeignKeys()))
            relationship.DeleteBehavior = DeleteBehavior.Restrict;
    }
}
