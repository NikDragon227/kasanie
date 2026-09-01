using Kasanie.Api.Application;
using Kasanie.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Kasanie.Api.Infrastructure;

public sealed class DevelopmentSeeder(
    AppDbContext db,
    UserManager<ApplicationUser> users,
    ITrainingPlanGenerator planGenerator,
    ILogger<DevelopmentSeeder> logger)
{
    public const string DemoPassword = "Kasanie-Demo-2026!";

    private static readonly (string Name, string Region)[] DemoMunicipalities =
    [
        ("Альметьевск", "Республика Татарстан"), ("Архангельск", "Архангельская область"),
        ("Астрахань", "Астраханская область"), ("Барнаул", "Алтайский край"),
        ("Белгород", "Белгородская область"), ("Брянск", "Брянская область"),
        ("Владивосток", "Приморский край"), ("Владикавказ", "Республика Северная Осетия — Алания"),
        ("Владимир", "Владимирская область"), ("Волгоград", "Волгоградская область"),
        ("Вологда", "Вологодская область"), ("Воронеж", "Воронежская область"),
        ("Екатеринбург", "Свердловская область"), ("Иваново", "Ивановская область"),
        ("Ижевск", "Удмуртская Республика"), ("Иркутск", "Иркутская область"),
        ("Казань", "Республика Татарстан"), ("Калининград", "Калининградская область"),
        ("Калуга", "Калужская область"), ("Кемерово", "Кемеровская область — Кузбасс"),
        ("Киров", "Кировская область"), ("Краснодар", "Краснодарский край"),
        ("Красноярск", "Красноярский край"), ("Курск", "Курская область"),
        ("Липецк", "Липецкая область"), ("Магнитогорск", "Челябинская область"),
        ("Махачкала", "Республика Дагестан"), ("Москва", "Москва"),
        ("Набережные Челны", "Республика Татарстан"), ("Нижний Новгород", "Нижегородская область"),
        ("Новокузнецк", "Кемеровская область — Кузбасс"), ("Новороссийск", "Краснодарский край"),
        ("Новосибирск", "Новосибирская область"), ("Омск", "Омская область"),
        ("Оренбург", "Оренбургская область"), ("Орёл", "Орловская область"),
        ("Пенза", "Пензенская область"), ("Пермь", "Пермский край"),
        ("Петрозаводск", "Республика Карелия"), ("Псков", "Псковская область"),
        ("Ростов-на-Дону", "Ростовская область"), ("Рязань", "Рязанская область"),
        ("Самара", "Самарская область"), ("Санкт-Петербург", "Санкт-Петербург"),
        ("Саратов", "Саратовская область"), ("Севастополь", "Севастополь"),
        ("Симферополь", "Республика Крым"), ("Смоленск", "Смоленская область"),
        ("Сочи", "Краснодарский край"), ("Ставрополь", "Ставропольский край"),
        ("Сургут", "Ханты-Мансийский автономный округ — Югра"), ("Тамбов", "Тамбовская область"),
        ("Тверь", "Тверская область"), ("Тольятти", "Самарская область"),
        ("Томск", "Томская область"), ("Тула", "Тульская область"),
        ("Тюмень", "Тюменская область"), ("Улан-Удэ", "Республика Бурятия"),
        ("Ульяновск", "Ульяновская область"), ("Уфа", "Республика Башкортостан"),
        ("Хабаровск", "Хабаровский край"), ("Чебоксары", "Чувашская Республика"),
        ("Челябинск", "Челябинская область"), ("Череповец", "Вологодская область"),
        ("Чита", "Забайкальский край"), ("Ярославль", "Ярославская область"),
        ("Зеленодольск", "Республика Татарстан")
    ];

    public async Task SeedAsync()
    {
        var municipalityNames = await db.Municipalities.Select(x => x.Name).ToHashSetAsync();
        var missingMunicipalities = DemoMunicipalities.Where(x => !municipalityNames.Contains(x.Name))
            .Select(x => new Municipality { Name = x.Name, Region = x.Region }).ToList();
        if (missingMunicipalities.Count > 0)
        {
            db.Municipalities.AddRange(missingMunicipalities);
            await db.SaveChangesAsync();
        }

        var playerUser = await EnsureUser("player@kasanie.local", Roles.Player);
        var coachUser = await EnsureUser("coach@kasanie.local", Roles.Coach);
        var parentUser = await EnsureUser("parent@kasanie.local", Roles.Parent);
        var organizerUser = await EnsureUser("organizer@kasanie.local", Roles.Organizer);
        var ownerUser = await EnsureUser("owner@kasanie.local", Roles.SchoolOwner);
        await EnsureUser("admin@kasanie.local", Roles.Admin);
        await EnsurePublicOrganizerProfile(organizerUser);
        await SeedPublicDiscoveryAsync(organizerUser);

        if (!await db.AssessmentDefinitions.AnyAsync())
        {
            AddAssessment("Спринт 30 м", "Скорость на короткой дистанции", "Разомнитесь. Пробегите 30 метров с высокого старта, зафиксируйте лучший результат из двух попыток.", "сек", SkillCategory.Speed, ScoringDirection.LowerIsBetter, 3.5m, 10m, 7.5m, 4.2m, 1);
            AddAssessment("Бег 6 минут", "Общая выносливость", "Бегите 6 минут в устойчивом темпе и измерьте преодолённую дистанцию.", "м", SkillCategory.Endurance, ScoringDirection.HigherIsBetter, 400, 2200, 700, 1800, 2);
            AddAssessment("Слалом с мячом", "Контроль мяча в движении", "Проведите мяч между шестью стойками и измерьте время.", "сек", SkillCategory.BallControl, ScoringDirection.LowerIsBetter, 6, 40, 28, 9, 3);
            AddAssessment("Точные передачи", "Точность коротких передач", "Выполните 20 передач в размеченную зону и укажите число точных.", "из 20", SkillCategory.Passing, ScoringDirection.HigherIsBetter, 0, 20, 5, 19, 4);
            AddAssessment("Удары в створ", "Точность завершения", "Выполните 10 ударов с контрольной точки и укажите попадания в створ.", "из 10", SkillCategory.Shooting, ScoringDirection.HigherIsBetter, 0, 10, 2, 10, 5);
            AddAssessment("Челночный бег 4×10", "Смена направления и координация", "Пробегите четыре отрезка по 10 метров, касаясь линии рукой.", "сек", SkillCategory.Agility, ScoringDirection.LowerIsBetter, 7, 20, 16, 8.5m, 6);
            await db.SaveChangesAsync();
        }

        if (!await db.Exercises.AnyAsync())
        {
            var catalog = new[]
            {
                Ex("Взрывные старты", "Короткие ускорения из разных положений.", "6 ускорений по 10–15 м. Полный отдых между повторами.", SkillCategory.Speed, 2, 15, "Фишки"),
                Ex("Интервальный бег", "Развитие общей выносливости.", "6 циклов: 2 минуты лёгкого бега, 1 минута активного.", SkillCategory.Endurance, 3, 24, "Секундомер"),
                Ex("Слалом обеими ногами", "Ведение и частые касания.", "Пройдите коридор из 8 фишек четырьмя способами.", SkillCategory.BallControl, 2, 20, "Мяч, 8 фишек"),
                Ex("Передачи в квадрат", "Точность первого паса.", "40 передач в квадрат 1×1 м с расстояния 8 м.", SkillCategory.Passing, 2, 18, "Мяч, мишень"),
                Ex("Удар после смещения", "Завершение после ведения.", "5 серий по 4 удара после смещения вправо и влево.", SkillCategory.Shooting, 3, 22, "Мяч, ворота, фишки"),
                Ex("Координационная лестница", "Частота ног и баланс.", "Выполните 6 паттернов, по 3 прохода каждого.", SkillCategory.Agility, 2, 16, "Координационная лестница"),
                Ex("Повторные ускорения", "Скоростная выносливость.", "2 серии по 6 ускорений 20 м, отдых 30 секунд.", SkillCategory.Speed, 4, 20, "Фишки"),
                Ex("Квадрат касаний", "Контроль в ограниченном пространстве.", "60 секунд непрерывных касаний, 5 серий.", SkillCategory.BallControl, 3, 15, "Мяч, 4 фишки"),
                Ex("Пас после разворота", "Ориентация корпуса и точность.", "Примите мяч, развернитесь и выполните 30 передач в цель.", SkillCategory.Passing, 3, 20, "Мяч, стенка или партнёр"),
                Ex("Планка и мобильность", "Общая физическая подготовка.", "3 круга: планка, боковая планка, выпады и мобильность голеностопа.", SkillCategory.Endurance, 1, 15, "Коврик"),
                Ex("Удары слабой ногой", "Уверенность слабой ногой.", "30 контролируемых ударов по секторам ворот.", SkillCategory.Shooting, 3, 20, "Мячи, ворота"),
                Ex("Реактивные смены направления", "Реакция и ловкость.", "По сигналу меняйте направление между четырьмя цветными фишками.", SkillCategory.Agility, 3, 18, "4 цветные фишки")
            };
            db.Exercises.AddRange(catalog);
            db.TrainingPrograms.Add(new TrainingProgram { Name = "База: 4 недели", Description = "Сбалансированное развитие техники и физических качеств", Weeks = 4 });
            db.AchievementDefinitions.AddRange(
                new AchievementDefinition { Code = "FIRST_ASSESSMENT", Name = "Точка отсчёта", Description = "Завершено первое тестирование" },
                new AchievementDefinition { Code = "FIRST_WORKOUT", Name = "Первый шаг", Description = "Завершена первая тренировка" });
            await db.SaveChangesAsync();
        }

        var municipality = await db.Municipalities.SingleAsync(x => x.Name == "Казань");
        var player = await db.Players.SingleOrDefaultAsync(x => x.UserId == playerUser.Id);
        if (player is null)
        {
            player = new PlayerProfile { UserId = playerUser.Id, FirstName = "Артём", LastName = "Соколов", DateOfBirth = new DateOnly(2010, 5, 12), MunicipalityId = municipality.Id, PreferredPosition = "Полузащитник", DominantFoot = "Правая", ExperienceLevel = "Любитель", Height = 168, Weight = 57 };
            db.Players.Add(player);
            await db.SaveChangesAsync();
        }

        var coach = await db.CoachProfiles.SingleOrDefaultAsync(x => x.UserId == coachUser.Id);
        if (coach is null) { coach = new CoachProfile { UserId = coachUser.Id, DisplayName = "Илья Морозов" }; db.CoachProfiles.Add(coach); }
        var parent = await db.ParentProfiles.SingleOrDefaultAsync(x => x.UserId == parentUser.Id);
        if (parent is null) { parent = new ParentProfile { UserId = parentUser.Id }; db.ParentProfiles.Add(parent); }
        await db.SaveChangesAsync();

        var child = await db.Players.SingleOrDefaultAsync(x => x.UserId == null && x.FirstName == "Миша" && x.LastName == "Волков");
        if (child is null)
        {
            child = new PlayerProfile { FirstName = "Миша", LastName = "Волков", DateOfBirth = new DateOnly(2014, 9, 3), MunicipalityId = municipality.Id, PreferredPosition = "Нападающий", DominantFoot = "Левая", ExperienceLevel = "Начинающий" };
            db.Players.Add(child);
            await db.SaveChangesAsync();
        }

        if (!await db.CoachPlayerLinks.AnyAsync(x => x.CoachId == coach.Id && x.PlayerId == player.Id)) db.CoachPlayerLinks.Add(new CoachPlayerLink { CoachId = coach.Id, PlayerId = player.Id });
        if (!await db.ParentPlayerLinks.AnyAsync(x => x.ParentId == parent.Id && x.PlayerId == child.Id)) db.ParentPlayerLinks.Add(new ParentPlayerLink { ParentId = parent.Id, PlayerId = child.Id, Relationship = "Отец", IsPrimary = true, ConsentAccepted = true, ConsentVersion = "demo-v1", ConsentAcceptedAt = DateTimeOffset.UtcNow.AddDays(-30) });
        await db.SaveChangesAsync();

        var school = await db.Schools.SingleOrDefaultAsync(x => x.Slug == "kasanie-demo");
        if (school is null)
        {
            school = new School { Name = "Касание Demo", Slug = "kasanie-demo", City = "Казань", ContactEmail = "owner@kasanie.local" };
            db.Schools.Add(school);
            await db.SaveChangesAsync();
        }
        if (!await db.SchoolMemberships.AnyAsync(x => x.SchoolId == school.Id && x.UserId == ownerUser.Id)) db.SchoolMemberships.Add(new SchoolMembership { SchoolId = school.Id, UserId = ownerUser.Id, Role = SchoolMembershipRole.Owner });
        if (!await db.SchoolMemberships.AnyAsync(x => x.SchoolId == school.Id && x.UserId == coachUser.Id)) db.SchoolMemberships.Add(new SchoolMembership { SchoolId = school.Id, UserId = coachUser.Id, Role = SchoolMembershipRole.Coach });
        var team = await db.Teams.FirstOrDefaultAsync(x => x.SchoolId == school.Id && (x.Name == "Основная группа" || (x.Name == "Первый состав" && x.AgeGroup == "U17")));
        if (team is null) { team = new Team { SchoolId = school.Id, Name = "Первый состав", AgeGroup = "U17", Season = "2026/27", TrainingCycleStage = "Соревновательный этап", CycleStart = new DateOnly(2026, 8, 1), CycleEnd = new DateOnly(2026, 11, 30) }; db.Teams.Add(team); }
        else { team.Name = "Первый состав"; team.AgeGroup = "U17"; team.Season ??= "2026/27"; team.TrainingCycleStage = "Соревновательный этап"; team.CycleStart ??= new DateOnly(2026, 8, 1); team.CycleEnd ??= new DateOnly(2026, 11, 30); team.CodeOfConduct ??= "Приходим вовремя. Уважаем партнёров и соперника. Ошибку разбираем, а не высмеиваем. Учёба важнее дополнительной нагрузки."; }
        await db.SaveChangesAsync();
        var duplicateDemoTeams = await db.Teams.Where(x => x.SchoolId == school.Id && x.Id != team.Id && x.IsActive && (x.Name == "Основная группа" || x.Name == "U17 - первый состав") && !x.TeamPlayers.Any(p => p.IsActive) && !db.TeamTrainings.Any(t => t.TeamId == x.Id)).ToListAsync();
        foreach (var duplicate in duplicateDemoTeams) duplicate.IsActive = false;
        var teamCoach = await db.TeamCoaches.SingleOrDefaultAsync(x => x.TeamId == team.Id && x.CoachId == coach.Id);
        if (teamCoach is null) db.TeamCoaches.Add(new TeamCoach { TeamId = team.Id, CoachId = coach.Id, IsHeadCoach = true });
        else teamCoach.IsHeadCoach = true;
        if (!await db.TeamPlayers.AnyAsync(x => x.TeamId == team.Id && x.PlayerId == player.Id)) db.TeamPlayers.Add(new TeamPlayer { TeamId = team.Id, PlayerId = player.Id, ShirtNumber = 10 });
        if (!await db.TeamPlayers.AnyAsync(x => x.TeamId == team.Id && x.PlayerId == child.Id)) db.TeamPlayers.Add(new TeamPlayer { TeamId = team.Id, PlayerId = child.Id, ShirtNumber = 9 });
        await db.SaveChangesAsync();

        if (!await db.SkillSnapshots.AnyAsync(x => x.PlayerId == player.Id))
        {
            await CreateHistory(player, [62, 55, 48, 70, 51, 58], DateTimeOffset.UtcNow.AddDays(-42));
            var latest = await CreateHistory(player, [69, 63, 57, 75, 60, 66], DateTimeOffset.UtcNow.AddDays(-7));
            var plan = planGenerator.Generate(player, latest, await db.Exercises.AsNoTracking().ToListAsync(), Dates.Monday(DateOnly.FromDateTime(DateTime.UtcNow)));
            db.TrainingPlans.Add(plan);
            db.PlayerAchievements.Add(new PlayerAchievement { PlayerId = player.Id, AchievementDefinitionId = (await db.AchievementDefinitions.FirstAsync(x => x.Code == "FIRST_ASSESSMENT")).Id, AwardedAt = DateTimeOffset.UtcNow.AddDays(-7) });
            await db.SaveChangesAsync();
        }

        var demoNames = new[] { "Демо 1", "Демо 2", "Демо 3", "Демо 4" };
        foreach (var name in demoNames)
        {
            if (await db.Players.AnyAsync(x => x.FirstName == name)) continue;
            db.Players.Add(new PlayerProfile { FirstName = name, LastName = "Регион", DateOfBirth = new DateOnly(2011, 1, 1), MunicipalityId = municipality.Id, PreferredPosition = "Защитник", DominantFoot = "Правая", ExperienceLevel = "Начинающий", CreatedAt = DateTimeOffset.UtcNow.AddDays(-15) });
        }
        await db.SaveChangesAsync();
        var demoPlayerIds = await db.Players.Select(x => x.Id).ToListAsync();
        var assignedPlayerIds = await db.TeamPlayers.Where(x => x.TeamId == team.Id).Select(x => x.PlayerId).ToListAsync();
        db.TeamPlayers.AddRange(demoPlayerIds.Except(assignedPlayerIds).Select(x => new TeamPlayer { TeamId = team.Id, PlayerId = x }));
        await db.SaveChangesAsync();
        var group = await db.TeamTrainingGroups.Include(x => x.Players).SingleOrDefaultAsync(x => x.TeamId == team.Id && x.Name == "Индивидуальная работа");
        if (group is null)
        {
            group = new TeamTrainingGroup { TeamId = team.Id, Name = "Индивидуальная работа", Purpose = "Техника и принятие решений" };
            db.TeamTrainingGroups.Add(group);
        }
        var groupPlayerIds = group.Players.Select(x => x.PlayerId).ToHashSet();
        foreach (var playerId in demoPlayerIds.Take(3).Where(x => !groupPlayerIds.Contains(x))) group.Players.Add(new TeamTrainingGroupPlayer { PlayerId = playerId });
        if (!await db.TeamMatches.AnyAsync(x => x.TeamId == team.Id)) db.TeamMatches.AddRange(
            new TeamMatch { TeamId = team.Id, Opponent = "Академия Рубин", Competition = "Первенство города", ScheduledAt = DateTimeOffset.UtcNow.Date.AddDays(5).AddHours(12), Venue = "Дома" },
            new TeamMatch { TeamId = team.Id, Opponent = "Смена", Competition = "Товарищеский матч", ScheduledAt = DateTimeOffset.UtcNow.Date.AddDays(-8).AddHours(15), Venue = "В гостях", Status = "Завершён", GoalsFor = 3, GoalsAgainst = 1 });
        var demoTournament = await db.TeamTournaments.FirstOrDefaultAsync(x => x.TeamId == team.Id);
        if (demoTournament is null)
        {
            db.TeamTournaments.Add(new TeamTournament { TeamId = team.Id, Name = "Кубок академий", StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(20)), EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(22)), RegistrationDeadline = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(12)), Status = "Подготовка", EntryFee = 15000, TravelCost = 24000, AccommodationCost = 36000, MealCost = 18000, Income = 10000 });
        }
        else if (demoTournament.RegistrationDeadline is null)
        {
            demoTournament.RegistrationDeadline = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(12));
        }
        if (!await db.TeamMessages.AnyAsync(x => x.TeamId == team.Id)) db.TeamMessages.AddRange(
            new TeamMessage { TeamId = team.Id, AuthorUserId = ownerUser.Id, Channel = TeamMessageChannel.Owner, Text = "Илья, подтвердите план подготовки к ближайшему матчу." },
            new TeamMessage { TeamId = team.Id, AuthorUserId = coachUser.Id, Channel = TeamMessageChannel.Team, Text = "Завтра сбор за 20 минут до начала. Возьмите обе игровые футболки." });
        if (!await db.TeamScheduleEvents.AnyAsync(x => x.TeamId == team.Id)) db.TeamScheduleEvents.AddRange(
            new TeamScheduleEvent { TeamId = team.Id, Type = "Собрание", Title = "Разбор следующего соперника", StartsAt = DateTimeOffset.UtcNow.Date.AddDays(2).AddHours(18), ReminderAt = DateTimeOffset.UtcNow.Date.AddDays(2).AddHours(12) },
            new TeamScheduleEvent { TeamId = team.Id, Type = "Регистрация", Title = "Закрыть заявку на Кубок академий", StartsAt = DateTimeOffset.UtcNow.Date.AddDays(12).AddHours(18) });
        if (!await db.TeamInjuries.AnyAsync(x => x.TeamId == team.Id)) db.TeamInjuries.Add(new TeamInjury { TeamId = team.Id, PlayerId = child.Id, Type = "Ушиб голеностопа", Severity = "Незначительная", Status = "Наблюдение", RiskLevel = 35, StartedOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)), ExpectedReturnOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)), Notes = "Ограничить прыжковую нагрузку." });
        await db.SaveChangesAsync();
        if (!await db.TeamTrainings.AnyAsync(x => x.TeamId == team.Id))
        {
            var journalExerciseIds = await db.Exercises.Where(x => x.IsActive).OrderBy(x => x.Id).Take(3).Select(x => x.Id).ToListAsync();
            var journal = new TeamTraining { TeamId = team.Id, CoachId = coach.Id, Title = "Техника и первый пас", ScheduledAt = DateTimeOffset.UtcNow.Date.AddHours(16).AddDays(1) };
            for (var i = 0; i < journalExerciseIds.Count; i++) journal.Exercises.Add(new TeamTrainingExercise { ExerciseId = journalExerciseIds[i], SortOrder = i + 1 });
            foreach (var playerId in demoPlayerIds) journal.Attendances.Add(new TeamTrainingAttendance { PlayerId = playerId });
            db.TeamTrainings.Add(journal);
            await db.SaveChangesAsync();
        }
        var journalExercises = await db.Exercises.Where(x => x.IsActive).OrderBy(x => x.Id).Take(3).ToListAsync();
        var topics = new[] { "Первый пас и открывание", "Скорость принятия решения", "Игра один в один", "Закрепление в малых составах" };
        for (var sessionIndex = 0; sessionIndex < topics.Length; sessionIndex++)
        {
            var topic = topics[sessionIndex];
            if (await db.TeamTrainings.AnyAsync(x => x.TeamId == team.Id && x.Title == topic)) continue;
            var completedAt = DateTimeOffset.UtcNow.Date.AddDays(-21 + sessionIndex * 6).AddHours(18);
            var journal = new TeamTraining { TeamId = team.Id, CoachId = coach.Id, Title = topic, ScheduledAt = completedAt.AddHours(-1), Status = TeamTrainingStatus.Completed, AttendanceSavedAt = completedAt.AddHours(-1), CompletedAt = completedAt, Notes = "DEMO: командная тренировка завершена" };
            foreach (var playerId in demoPlayerIds)
            {
                var status = sessionIndex == 2 && playerId % 5 == 0 ? AttendanceStatus.Absent : sessionIndex == 1 && playerId % 4 == 0 ? AttendanceStatus.Late : AttendanceStatus.Present;
                journal.Attendances.Add(new TeamTrainingAttendance { PlayerId = playerId, Status = status });
            }
            for (var exerciseIndex = 0; exerciseIndex < journalExercises.Count; exerciseIndex++)
            {
                var item = new TeamTrainingExercise { ExerciseId = journalExercises[exerciseIndex].Id, SortOrder = exerciseIndex + 1 };
                foreach (var attendance in journal.Attendances.Where(x => x.Status is AttendanceStatus.Present or AttendanceStatus.Late))
                {
                    item.PlayerResults.Add(new TeamTrainingPlayerResult
                    {
                        PlayerId = attendance.PlayerId,
                        IsCompleted = !(sessionIndex == 2 && exerciseIndex == attendance.PlayerId % journalExercises.Count),
                        Understood = !(sessionIndex == 0 && exerciseIndex == attendance.PlayerId % journalExercises.Count),
                        UpdatedAt = completedAt
                    });
                }
                journal.Exercises.Add(item);
            }
            db.TeamTrainings.Add(journal);
        }
        await db.SaveChangesAsync();
        var profilesWithoutHistory = await db.Players.Where(x => !db.SkillSnapshots.Any(s => s.PlayerId == x.Id)).ToListAsync();
        foreach (var profile in profilesWithoutHistory)
        {
            var baseScore = 48 + profile.Id % 12;
            var snapshot = await CreateHistory(profile, [baseScore + 4, baseScore, baseScore + 7, baseScore + 10, baseScore + 2, baseScore + 5], DateTimeOffset.UtcNow.AddDays(-(profile.Id % 6)));
            var generatedPlan = planGenerator.Generate(profile, snapshot, await db.Exercises.AsNoTracking().ToListAsync(), Dates.Monday(DateOnly.FromDateTime(DateTime.UtcNow)));
            db.TrainingPlans.Add(generatedPlan);
            await db.SaveChangesAsync();
            var firstDay = generatedPlan.Days[0];
            var completedSession = new TrainingSession { PlayerId = profile.Id, TrainingDayId = firstDay.Id, Status = SessionStatus.Completed, StartedAt = DateTimeOffset.UtcNow.AddDays(-2), CompletedAt = DateTimeOffset.UtcNow.AddDays(-2).AddMinutes(55), Notes = "DEMO history" };
            foreach (var item in firstDay.Exercises) completedSession.Results.Add(new TrainingExerciseResult { TrainingExerciseId = item.Id, IsCompleted = true, DurationMinutes = item.TargetDurationMinutes, PerceivedDifficulty = 3, CompletedAt = completedSession.CompletedAt });
            db.TrainingSessions.Add(completedSession);
            await db.SaveChangesAsync();
        }
        logger.LogInformation("Development demo data is ready. Demo password: {DemoPasswordMarker}", "configured in README (development only)");
    }

    private async Task SeedPublicDiscoveryAsync(ApplicationUser organizer)
    {
        var football = await db.Sports.SingleOrDefaultAsync(x => x.Slug == "football");
        if (football is null)
        {
            football = new Sport { Slug = "football", Name = "Футбол" };
            db.Sports.Add(football);
            await db.SaveChangesAsync();
        }

        var venue = await db.SportsVenues.SingleOrDefaultAsync(x => x.Slug == "centralny-stadion-kazan");
        if (venue is null)
        {
            venue = new SportsVenue
            {
                Slug = "centralny-stadion-kazan", Name = "Центральная футбольная площадка", Region = "Республика Татарстан",
                City = "Казань", District = "Вахитовский район", Address = "ул. Ташаяк, 2А", Latitude = 55.7963,
                Longitude = 49.0999, SurfaceType = "Искусственный газон", HasChangingRooms = true, HasLighting = true,
                HasParking = true, IsVerified = true, Description = "Освещённое поле для игр и групповых тренировок."
            };
            db.SportsVenues.Add(venue);
            await db.SaveChangesAsync();
        }

        var demoSlugs = new[] { "football-6x6-evening-kazan", "group-ball-control-training-kazan", "coach-speed-training-kazan" };
        var existingDemoActivities = await db.PublicActivities.Where(x => demoSlugs.Contains(x.Slug)).ToListAsync();
        if (existingDemoActivities.Count > 0)
        {
            foreach (var activity in existingDemoActivities)
            {
                activity.OrganizerId = organizer.Id;
                activity.GameFormat ??= "6×6";
            }
            await db.SaveChangesAsync();
            return;
        }
        var tomorrow = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero).AddDays(1);
        db.PublicActivities.AddRange(
            new PublicActivity
            {
                Slug = "football-6x6-evening-kazan", SportId = football.Id, SportsVenueId = venue.Id, OrganizerId = organizer.Id,
                EventType = PublicActivityType.Game, GameFormat = "6×6", Title = "Футбол 6×6 вечером", Description = "Собираем две равные команды. Манишки и мячи предоставит организатор.",
                StartAt = tomorrow.AddHours(16), EndAt = tomorrow.AddHours(17.5), Capacity = 12, WaitlistCapacity = 4, Price = 500,
                SkillLevel = "Любитель", Status = PublicActivityStatus.Published, PublishedAt = DateTimeOffset.UtcNow,
                RegistrationDeadline = tomorrow.AddHours(14), Rules = "Приходите за 15 минут до начала."
            },
            new PublicActivity
            {
                Slug = "group-ball-control-training-kazan", SportId = football.Id, SportsVenueId = venue.Id, OrganizerId = organizer.Id,
                EventType = PublicActivityType.GroupTraining, GameFormat = "6×6", Title = "Совместная тренировка: контроль мяча", Description = "Открытая тренировка для взрослых: техника, первый пас и небольшая игра в конце.",
                StartAt = tomorrow.AddDays(2).AddHours(15), EndAt = tomorrow.AddDays(2).AddHours(16.5), Capacity = 10, WaitlistCapacity = 3, Price = 0,
                SkillLevel = "Любой", Status = PublicActivityStatus.Published, PublishedAt = DateTimeOffset.UtcNow,
                EquipmentRequirements = "Бутсы и вода", Rules = "Без опозданий; сообщите об отмене заранее."
            },
            new PublicActivity
            {
                Slug = "coach-speed-training-kazan", SportId = football.Id, SportsVenueId = venue.Id, OrganizerId = organizer.Id,
                EventType = PublicActivityType.CoachTraining, GameFormat = "6×6", Title = "Скорость и первый шаг с тренером", Description = "Групповая тренировка с тренером: стартовая скорость, координация и работа с мячом.",
                StartAt = tomorrow.AddDays(4).AddHours(17), EndAt = tomorrow.AddDays(4).AddHours(18.25), Capacity = 8, WaitlistCapacity = 2, Price = 900,
                SkillLevel = "Любитель", Status = PublicActivityStatus.Published, PublishedAt = DateTimeOffset.UtcNow,
                EquipmentRequirements = "Форма по погоде и вода"
            });
        await db.SaveChangesAsync();
    }

    private async Task EnsurePublicOrganizerProfile(ApplicationUser organizer)
    {
        if (await db.PublicOrganizerProfiles.AnyAsync(x => x.UserId == organizer.Id)) return;
        var municipality = await db.Municipalities.SingleAsync(x => x.Name == "Казань");
        db.PublicOrganizerProfiles.Add(new PublicOrganizerProfile
        {
            UserId = organizer.Id,
            DisplayName = "Организатор Касания",
            DateOfBirth = new DateOnly(1990, 1, 1),
            MunicipalityId = municipality.Id
        });
        await db.SaveChangesAsync();
    }

    private async Task<ApplicationUser> EnsureUser(string email, string role)
    {
        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
            var result = await users.CreateAsync(user, DemoPassword);
            if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description)));
        }
        if (!await users.IsInRoleAsync(user, role)) await users.AddToRoleAsync(user, role);
        return user;
    }

    private void AddAssessment(string name, string description, string instructions, string unit, SkillCategory category, ScoringDirection direction, decimal min, decimal max, decimal low, decimal high, int order)
    {
        var definition = new AssessmentDefinition { Name = name, Description = description, Instructions = instructions, Unit = unit, SkillCategory = category, ScoringDirection = direction, MinimumReasonableValue = min, MaximumReasonableValue = max, SortOrder = order };
        db.AssessmentDefinitions.Add(definition);
        db.AssessmentNorms.Add(new AssessmentNorm { AssessmentDefinition = definition, MinimumAge = 6, MaximumAge = 99, LowPerformanceValue = low, HighPerformanceValue = high, IsDemo = true, SourceNote = "DEMO: условная шкала для проверки продукта; не является научно валидированным нормативом." });
    }

    private static Exercise Ex(string name, string description, string instructions, SkillCategory category, int difficulty, int duration, string equipment) => new() { Name = name, Description = description, Instructions = instructions, SkillCategory = category, Difficulty = difficulty, DurationMinutes = duration, Equipment = equipment };

    private async Task<SkillSnapshot> CreateHistory(PlayerProfile player, int[] scores, DateTimeOffset capturedAt)
    {
        var session = new AssessmentSession { PlayerId = player.Id, IsCompleted = true, StartedAt = capturedAt.AddMinutes(-20), CompletedAt = capturedAt };
        db.AssessmentSessions.Add(session);
        await db.SaveChangesAsync();
        var snapshot = new SkillSnapshot { PlayerId = player.Id, AssessmentSessionId = session.Id, Speed = scores[0], Endurance = scores[1], BallControl = scores[2], Passing = scores[3], Shooting = scores[4], Agility = scores[5], CapturedAt = capturedAt };
        db.SkillSnapshots.Add(snapshot);
        await db.SaveChangesAsync();
        return snapshot;
    }
}
