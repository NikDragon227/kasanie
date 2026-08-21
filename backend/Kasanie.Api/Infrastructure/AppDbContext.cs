using Kasanie.Api.Domain;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Kasanie.Api.Infrastructure;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Municipality> Municipalities => Set<Municipality>();
    public DbSet<PlayerProfile> Players => Set<PlayerProfile>();
    public DbSet<ParentProfile> ParentProfiles => Set<ParentProfile>();
    public DbSet<ParentPlayerLink> ParentPlayerLinks => Set<ParentPlayerLink>();
    public DbSet<CoachProfile> CoachProfiles => Set<CoachProfile>();
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
    public DbSet<CoachNote> CoachNotes => Set<CoachNote>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasPostgresEnum<SkillCategory>();

        builder.Entity<PlayerProfile>().HasIndex(x => x.UserId).IsUnique();
        builder.Entity<ParentProfile>().HasIndex(x => x.UserId).IsUnique();
        builder.Entity<CoachProfile>().HasIndex(x => x.UserId).IsUnique();
        builder.Entity<ParentPlayerLink>().HasKey(x => new { x.ParentId, x.PlayerId });
        builder.Entity<CoachPlayerLink>().HasKey(x => new { x.CoachId, x.PlayerId });
        builder.Entity<TrainingProgramExercise>().HasKey(x => new { x.TrainingProgramId, x.ExerciseId });
        builder.Entity<AssessmentResult>().HasIndex(x => new { x.AssessmentSessionId, x.AssessmentDefinitionId }).IsUnique();
        builder.Entity<TrainingSession>().HasIndex(x => new { x.PlayerId, x.TrainingDayId }).IsUnique();
        builder.Entity<TrainingExerciseResult>().HasIndex(x => new { x.TrainingSessionId, x.TrainingExerciseId }).IsUnique();
        builder.Entity<AchievementDefinition>().HasIndex(x => x.Code).IsUnique();

        builder.Entity<PlayerProfile>().Property(x => x.Height).HasPrecision(5, 1);
        builder.Entity<PlayerProfile>().Property(x => x.Weight).HasPrecision(5, 1);
        builder.Entity<AssessmentDefinition>().Property(x => x.MinimumReasonableValue).HasPrecision(10, 2);
        builder.Entity<AssessmentDefinition>().Property(x => x.MaximumReasonableValue).HasPrecision(10, 2);
        builder.Entity<AssessmentNorm>().Property(x => x.LowPerformanceValue).HasPrecision(10, 2);
        builder.Entity<AssessmentNorm>().Property(x => x.HighPerformanceValue).HasPrecision(10, 2);

        foreach (var relationship in builder.Model.GetEntityTypes().SelectMany(x => x.GetForeignKeys()))
            relationship.DeleteBehavior = DeleteBehavior.Restrict;
    }
}
