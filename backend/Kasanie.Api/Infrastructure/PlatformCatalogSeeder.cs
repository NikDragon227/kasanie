using Kasanie.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Kasanie.Api.Infrastructure;

public sealed class PlatformCatalogSeeder(AppDbContext db)
{
    private static readonly (string Name, string Region)[] RussianCities =
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

    private static readonly (string Slug, string Name)[] Sports =
    [
        ("football", "Футбол"), ("basketball", "Баскетбол"),
        ("volleyball", "Волейбол"), ("running", "Бег"), ("hockey", "Хоккей"),
        ("tennis", "Теннис"), ("badminton", "Бадминтон"),
        ("workout", "Функциональные тренировки")
    ];

    private static readonly ExerciseTemplate[] Exercises =
    [
        new("Взрывные старты", "Короткие ускорения из разных положений.", "6 ускорений по 10–15 м. Полный отдых между повторами.", SkillCategory.Speed, 2, 15, "Фишки"),
        new("Интервальный бег", "Развитие общей выносливости.", "6 циклов: 2 минуты лёгкого бега, 1 минута активного.", SkillCategory.Endurance, 3, 24, "Секундомер"),
        new("Слалом обеими ногами", "Ведение и частые касания.", "Пройдите коридор из 8 фишек четырьмя способами.", SkillCategory.BallControl, 2, 20, "Мяч, 8 фишек"),
        new("Передачи в квадрат", "Точность первого паса.", "40 передач в квадрат 1×1 м с расстояния 8 м.", SkillCategory.Passing, 2, 18, "Мяч, мишень"),
        new("Удар после смещения", "Завершение после ведения.", "5 серий по 4 удара после смещения вправо и влево.", SkillCategory.Shooting, 3, 22, "Мяч, ворота, фишки"),
        new("Координационная лестница", "Частота ног и баланс.", "Выполните 6 паттернов, по 3 прохода каждого.", SkillCategory.Agility, 2, 16, "Координационная лестница"),
        new("Повторные ускорения", "Скоростная выносливость.", "2 серии по 6 ускорений 20 м, отдых 30 секунд.", SkillCategory.Speed, 4, 20, "Фишки"),
        new("Квадрат касаний", "Контроль в ограниченном пространстве.", "60 секунд непрерывных касаний, 5 серий.", SkillCategory.BallControl, 3, 15, "Мяч, 4 фишки"),
        new("Пас после разворота", "Ориентация корпуса и точность.", "Примите мяч, развернитесь и выполните 30 передач в цель.", SkillCategory.Passing, 3, 20, "Мяч, стенка или партнёр"),
        new("Планка и мобильность", "Общая физическая подготовка.", "3 круга: планка, боковая планка, выпады и мобильность голеностопа.", SkillCategory.Endurance, 1, 15, "Коврик"),
        new("Удары слабой ногой", "Уверенность слабой ногой.", "30 контролируемых ударов по секторам ворот.", SkillCategory.Shooting, 3, 20, "Мячи, ворота"),
        new("Реактивные смены направления", "Реакция и ловкость.", "По сигналу меняйте направление между четырьмя цветными фишками.", SkillCategory.Agility, 3, 18, "4 цветные фишки")
    ];

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var existingCityNames = await db.Municipalities.AsNoTracking().Select(x => x.Name).ToHashSetAsync(cancellationToken);
        var missingCities = RussianCities.Where(x => !existingCityNames.Contains(x.Name))
            .Select(x => new Municipality { Name = x.Name, Region = x.Region }).ToList();
        if (missingCities.Count > 0)
        {
            db.Municipalities.AddRange(missingCities);
            await db.SaveChangesAsync(cancellationToken);
        }

        var existingSportSlugs = await db.Sports.AsNoTracking().Select(x => x.Slug).ToHashSetAsync(cancellationToken);
        var missingSports = Sports.Where(x => !existingSportSlugs.Contains(x.Slug))
            .Select(x => new Sport { Slug = x.Slug, Name = x.Name }).ToList();
        if (missingSports.Count > 0)
        {
            db.Sports.AddRange(missingSports);
            await db.SaveChangesAsync(cancellationToken);
        }

        var existingNames = await db.Exercises.AsNoTracking()
            .Select(x => x.Name)
            .ToHashSetAsync(cancellationToken);
        var missing = Exercises.Where(x => !existingNames.Contains(x.Name)).Select(x => x.ToEntity()).ToList();
        if (missing.Count == 0) return;

        db.Exercises.AddRange(missing);
        await db.SaveChangesAsync(cancellationToken);
    }

    private sealed record ExerciseTemplate(
        string Name,
        string Description,
        string Instructions,
        SkillCategory SkillCategory,
        int Difficulty,
        int DurationMinutes,
        string Equipment)
    {
        public Exercise ToEntity() => new()
        {
            Name = Name,
            Description = Description,
            Instructions = Instructions,
            SkillCategory = SkillCategory,
            Difficulty = Difficulty,
            DurationMinutes = DurationMinutes,
            Equipment = Equipment
        };
    }
}
