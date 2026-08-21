using Kasanie.Api.Application;
using Kasanie.Api.Domain;

namespace Kasanie.Tests;

public sealed class AlgorithmTests
{
    [Fact]
    public void AssessmentScorer_RespectsBothDirectionsAndClamps()
    {
        var scorer = new AssessmentScorer();
        var norm = new AssessmentNorm { MinimumAge = 10, MaximumAge = 18, LowPerformanceValue = 10, HighPerformanceValue = 20, SourceNote = "test" };
        var higher = Definition(ScoringDirection.HigherIsBetter);
        Assert.Equal(50, scorer.Calculate(15, higher, norm));
        Assert.Equal(0, scorer.Calculate(5, higher, norm));
        Assert.Equal(100, scorer.Calculate(25, higher, norm));
        norm.LowPerformanceValue = 20; norm.HighPerformanceValue = 10;
        Assert.Equal(50, scorer.Calculate(15, Definition(ScoringDirection.LowerIsBetter), norm));
    }

    [Fact]
    public void AssessmentResultCollection_UpdatesSavedDraftWithoutReplacingRequiredChild()
    {
        var savedDraft = new AssessmentResult { Id = 42, AssessmentDefinitionId = 7, RawValue = 11 };
        var session = new AssessmentSession { Id = 3, PlayerId = 1, Results = [savedDraft] };

        var updated = AssessmentResultCollection.Upsert(session, 7, 10.5m, 73);

        Assert.Same(savedDraft, updated);
        Assert.Single(session.Results);
        Assert.Equal(10.5m, savedDraft.RawValue);
        Assert.Equal(73, savedDraft.NormalizedScore);
    }

    [Fact]
    public void TrainingPlanGenerator_PrioritizesWeakSkillsAndAvoidsDuplicatesPerDay()
    {
        var player = new PlayerProfile { Id = 1, FirstName = "Иван", LastName = "Тестов", DateOfBirth = new(2010, 1, 1), MunicipalityId = 1, PreferredPosition = "Нападающий", DominantFoot = "Правая", ExperienceLevel = "Любитель" };
        var snapshot = new SkillSnapshot { Speed = 70, Endurance = 65, BallControl = 20, Passing = 80, Shooting = 25, Agility = 75 };
        var exercises = Enum.GetValues<SkillCategory>().SelectMany((category, index) => Enumerable.Range(1, 2).Select(n => new Exercise { Id = index * 2 + n, Name = $"{category}-{n}", Description = "d", Instructions = "i", SkillCategory = category, Difficulty = 2, DurationMinutes = 15, Equipment = "none" })).ToList();
        var plan = new TrainingPlanGenerator().Generate(player, snapshot, exercises, new DateOnly(2026, 8, 10));
        Assert.Equal(3, plan.Days.Count);
        Assert.All(plan.Days, day => Assert.Equal(day.Exercises.Count, day.Exercises.Select(x => x.ExerciseId).Distinct().Count()));
        Assert.Contains("Контроль мяча", plan.GenerationReason);
        Assert.Contains("Удары", plan.GenerationReason);
        Assert.Contains(plan.Days[0].Exercises, x => exercises.Single(e => e.Id == x.ExerciseId).SkillCategory == SkillCategory.BallControl);
    }

    [Theory]
    [InlineData("2012-08-14", false)]
    [InlineData("2012-08-13", true)]
    [InlineData("2000-01-01", true)]
    public void Under14Policy_IsExact(string birthday, bool expected)
    {
        Assert.Equal(expected, AgePolicy.CanRegisterIndependently(DateOnly.Parse(birthday), new DateOnly(2026, 8, 13)));
    }

    private static AssessmentDefinition Definition(ScoringDirection direction) => new() { Name = "test", Description = "test", Instructions = "test", Unit = "u", SkillCategory = SkillCategory.Speed, ScoringDirection = direction, MinimumReasonableValue = 0, MaximumReasonableValue = 100 };
}
