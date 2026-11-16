using ProjectK.Common.Entities.AgendaModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Entities.MemberModule;
using ProjectK.Common.Enums.AgendaModule;
using ProjectK.Common.Enums.MemberModule;

namespace ProjectK.Infrastructure.Seeding.Demo;

/// <summary>The calendar and the task board: сходини, табори and заходи with RSVPs, and tasks.</summary>
public partial class DemoDataSeeder
{
    private void SeedAgenda(
        Guid kurinKey,
        IReadOnlyDictionary<string, Group> groups,
        IReadOnlyDictionary<string, Leadership> leaderships,
        IReadOnlyList<Person> youth,
        IReadOnlyDictionary<Guid, Person> mentors,
        Person zvyazkovyi,
        IReadOnlyDictionary<LeadershipRole, Person> kurinOffices)
    {
        // The seven closed colours the category manager offers (BRANDBOOK §2).
        var skhodyny = Category(kurinKey, "Сходини", "#2F855A", "pi pi-users", rsvp: false, duration: 120, reminder: 60);
        // Every гурток's сходини are one schedule the whole kurin sees (AGENDA-03).
        skhodyny.IsKurinSchedule = true;
        var tabory = Category(kurinKey, "Табори", "#B7791F", "pi pi-sun", rsvp: true, duration: null, reminder: 24 * 60,
            capacity: 30, waitlist: true, template: "Що взяти: спальник, каремат, посуд, документи, гроші на дорогу.");
        var zakhody = Category(kurinKey, "Заходи", "#2B6CB0", "pi pi-flag", rsvp: true, duration: 180, reminder: 120);
        var vyshkoly = Category(kurinKey, "Вишколи", "#0E7490", "pi pi-compass", rsvp: true, duration: 240, reminder: 24 * 60);
        var sviata = Category(kurinKey, "Свята", "#6B46C1", "pi pi-star", rsvp: false, duration: 150, reminder: 60);
        var pratsia = Category(kurinKey, "Праця", "#4A5568", "pi pi-briefcase", rsvp: false, duration: 180, reminder: null);

        var kurinnyi = kurinOffices.GetValueOrDefault(LeadershipRole.Kurinnuy) ?? youth[0];
        var pysar = kurinOffices.GetValueOrDefault(LeadershipRole.Pysar) ?? youth[1];
        var hronikar = kurinOffices.GetValueOrDefault(LeadershipRole.Hronikar) ?? youth[2];
        var skarbnyk = kurinOffices.GetValueOrDefault(LeadershipRole.Skarbnyk) ?? youth[3];

        var sokoly = groups[Sokoly];
        var levy = groups["Леви"];
        var vedmedi = groups["Ведмеді"];

        var year = _now.Year;

        // Weekly сходини: the kurin on Saturdays, each гурток on its own weekday.
        Weekly(kurinKey, "Сходини куреня", "Збір усіх гуртків у домівці. Однострій.", skhodyny, zvyazkovyi,
            DayOfWeek.Saturday, 10, 120, weeksBack: 12, Target(AgendaTargetType.Kurin, kurinKey));
        Weekly(kurinKey, "Сходини гуртка «Соколи»", null, skhodyny, mentors[sokoly.GroupKey],
            DayOfWeek.Wednesday, 17, 90, weeksBack: 10, Target(AgendaTargetType.Group, sokoly.GroupKey));
        Weekly(kurinKey, "Сходини гуртка «Леви»", null, skhodyny, mentors[levy.GroupKey],
            DayOfWeek.Friday, 16, 90, weeksBack: 10, Target(AgendaTargetType.Group, levy.GroupKey));

        // Last summer's camp, already happened; everybody answered back then.
        var summerCamp = AllDay(kurinKey, "Літній табір «Соколине гніздо»",
            "Десять днів в Арденнах біля Вільца. Виїзд о 7:00 від домівки.",
            tabory, zvyazkovyi, UtcDay(year, 7, 10), UtcDay(year, 7, 20),
            Target(AgendaTargetType.Kurin, kurinKey));
        Respond(summerCamp, youth, going: 0.85, maybe: 0.0, notGoing: 0.1, daysAgoMin: 80, daysAgoMax: 120);

        // The winter camp is ahead; RSVPs are still coming in.
        var winterCamp = AllDay(kurinKey, "Зимовий табір «Сніговий шлях»",
            "Вогези, Жерармер. Лижі й снігоступи — свої або орендовані на місці.",
            tabory, zvyazkovyi, UtcDay(year + 1, 1, 3), UtcDay(year + 1, 1, 9),
            Target(AgendaTargetType.Kurin, kurinKey));
        Respond(winterCamp, youth, going: 0.6, maybe: 0.15, notGoing: 0.1, daysAgoMin: 1, daysAgoMax: 12);

        var hike = AllDay(kurinKey, "Мандрівка Мюллерталем",
            "Два дні стежкою Мюллерталь, ночівля в кемпінгу в Бердорфі. Старші гуртки.",
            zakhody, mentors[sokoly.GroupKey], NextDate(10, 10), NextDate(10, 11),
            Target(AgendaTargetType.Group, sokoly.GroupKey), Target(AgendaTargetType.Group, vedmedi.GroupKey));
        Respond(hike, youth.Where(p => p.GroupKey == sokoly.GroupKey || p.GroupKey == vedmedi.GroupKey).ToList(),
            going: 0.7, maybe: 0.1, notGoing: 0.1, daysAgoMin: 0, daysAgoMax: 6);

        Timed(kurinKey, "Пластова ватра до Дня Незалежності", "У парку Мерль, з батьками.",
            zakhody, zvyazkovyi, UtcDay(year, 8, 24), 19, 150, Target(AgendaTargetType.Kurin, kurinKey));
        Timed(kurinKey, "Свято Миколая", "Вистава молодших гуртків для батьків, після — спільний стіл.",
            sviata, zvyazkovyi, UtcDay(year, 12, 19), 17, 150, Target(AgendaTargetType.Kurin, kurinKey));
        Timed(kurinKey, "Вишкіл гурткових", "Планування року, ведення гурткової хроніки, робота з молодшими.",
            vyshkoly, zvyazkovyi, _now.Date.AddDays(14), 11, 300,
            Target(AgendaTargetType.Leadership, leaderships["Kurin"].LeadershipKey),
            Target(AgendaTargetType.Leadership, leaderships[Sokoly].LeadershipKey),
            Target(AgendaTargetType.Leadership, leaderships["Леви"].LeadershipKey));
        Timed(kurinKey, "Збірка КВ", "Підсумки вересня, табір, вкладка.",
            zakhody, zvyazkovyi, NextWeekday(DayOfWeek.Tuesday), 19, 90,
            Target(AgendaTargetType.Leadership, leaderships["KV"].LeadershipKey));
        Timed(kurinKey, "Прибирання домівки", "Після ремонту в підвалі. Рукавиці свої.",
            pratsia, mentors[levy.GroupKey], _now.Date.AddDays(-6), 15, 180, Target(AgendaTargetType.Group, levy.GroupKey));

        // The task board: something in every column, with the kind of owners a провід actually has.
        BoardTask(kurinKey, "Підготувати програму зимового табору", "Три дні — виховна частина, чотири — лижі. Погодити з КВ до кінця місяця.",
            AgendaItemStatus.InProgress, zvyazkovyi, due: _now.Date.AddDays(30), Target(AgendaTargetType.Leadership, leaderships["Kurin"].LeadershipKey));
        BoardTask(kurinKey, "Зібрати вкладку за вересень", "Гурткові скарбники здають курінному скарбнику на сходинах.",
            AgendaItemStatus.Todo, kurinnyi, due: _now.Date.AddDays(9),
            Target(AgendaTargetType.Group, sokoly.GroupKey), Target(AgendaTargetType.Group, levy.GroupKey), Target(AgendaTargetType.Group, vedmedi.GroupKey));
        BoardTask(kurinKey, "Подати список на зимовий табір у станицю", "Разом із медичними довідками.",
            AgendaItemStatus.Todo, zvyazkovyi, due: _now.Date.AddDays(12), Target(AgendaTargetType.Member, zvyazkovyi.Key));
        BoardTask(kurinKey, "Написати хроніку літнього табору", "Фото — від Остапа, текст — по одному дню на сторінку.",
            AgendaItemStatus.InProgress, kurinnyi, due: _now.Date.AddDays(5), Target(AgendaTargetType.Member, hronikar.Key));
        BoardTask(kurinKey, "Оновити склад куреня в реєстрі", "Двоє нових у Левах, один вибув.",
            AgendaItemStatus.Done, zvyazkovyi, due: _now.Date.AddDays(-3), Target(AgendaTargetType.Member, pysar.Key));
        BoardTask(kurinKey, "Замовити дрова для ватри", null,
            AgendaItemStatus.Done, kurinnyi, due: _now.Date.AddDays(-30), Target(AgendaTargetType.Member, skarbnyk.Key));
        BoardTask(kurinKey, "Перевірити табірне спорядження", "Намети, казани, сокири. Список — у господаря.",
            AgendaItemStatus.Todo, kurinnyi, due: null, Target(AgendaTargetType.Group, vedmedi.GroupKey));
        BoardTask(kurinKey, "Купити прапорці для теренової гри", null,
            AgendaItemStatus.Done, mentors[sokoly.GroupKey], due: _now.Date.AddDays(-12), Target(AgendaTargetType.Group, sokoly.GroupKey));
    }

    private AgendaCategory Category(
        Guid kurinKey,
        string name,
        string color,
        string icon,
        bool rsvp,
        int? duration,
        int? reminder,
        int? capacity = null,
        bool waitlist = false,
        string? template = null)
    {
        var category = new AgendaCategory
        {
            KurinKey = kurinKey,
            Name = name,
            ColorHex = color,
            Icon = icon,
            RsvpRequired = rsvp,
            DefaultDurationMinutes = duration,
            ReminderLeadMinutes = reminder,
            Capacity = capacity,
            WaitlistEnabled = waitlist,
            DefaultDescription = template
        };
        _dbContext.AgendaCategories.Add(category);
        return category;
    }

    private static AgendaAssignment Target(AgendaTargetType type, Guid key)
        => new() { TargetType = type, TargetKey = key };

    private void Weekly(
        Guid kurinKey,
        string title,
        string? description,
        AgendaCategory category,
        Person author,
        DayOfWeek day,
        int hour,
        int minutes,
        int weeksBack,
        params AgendaAssignment[] targets)
    {
        var first = PreviousOrSame(_now.Date, day).AddDays(-7 * weeksBack);
        var start = ToUtc(first, hour);
        _dbContext.AgendaItems.Add(new AgendaItem
        {
            KurinKey = kurinKey,
            Kind = AgendaItemKind.Event,
            Title = title,
            Description = description,
            StartUtc = start,
            EndUtc = start.AddMinutes(minutes),
            IsAllDay = false,
            AgendaCategoryKey = category.AgendaCategoryKey,
            RecurrenceFrequency = RecurrenceFrequency.Weekly,
            RecurrenceInterval = 1,
            RecurrenceByWeekday = 1 << (int)day,
            CreatedByUserKey = author.UserKey,
            Assignments = targets.ToList()
        });
    }

    private AgendaItem AllDay(
        Guid kurinKey,
        string title,
        string? description,
        AgendaCategory category,
        Person author,
        DateTime firstDay,
        DateTime lastDay,
        params AgendaAssignment[] targets)
    {
        var item = new AgendaItem
        {
            KurinKey = kurinKey,
            Kind = AgendaItemKind.Event,
            Title = title,
            Description = description,
            StartUtc = DateTime.SpecifyKind(firstDay.Date, DateTimeKind.Utc),
            EndUtc = DateTime.SpecifyKind(lastDay.Date, DateTimeKind.Utc).AddHours(21),
            IsAllDay = true,
            AgendaCategoryKey = category.AgendaCategoryKey,
            CreatedByUserKey = author.UserKey,
            Assignments = targets.ToList()
        };
        _dbContext.AgendaItems.Add(item);
        return item;
    }

    private void Timed(
        Guid kurinKey,
        string title,
        string? description,
        AgendaCategory category,
        Person author,
        DateTime day,
        int hour,
        int minutes,
        params AgendaAssignment[] targets)
    {
        var start = ToUtc(day.Date, hour);
        _dbContext.AgendaItems.Add(new AgendaItem
        {
            KurinKey = kurinKey,
            Kind = AgendaItemKind.Event,
            Title = title,
            Description = description,
            StartUtc = start,
            EndUtc = start.AddMinutes(minutes),
            IsAllDay = false,
            AgendaCategoryKey = category.AgendaCategoryKey,
            CreatedByUserKey = author.UserKey,
            Assignments = targets.ToList()
        });
    }

    private void BoardTask(
        Guid kurinKey,
        string title,
        string? description,
        AgendaItemStatus status,
        Person author,
        DateTime? due,
        params AgendaAssignment[] targets)
    {
        var start = due is null ? (DateTime?)null : DateTime.SpecifyKind(due.Value.Date, DateTimeKind.Utc);
        _dbContext.AgendaItems.Add(new AgendaItem
        {
            KurinKey = kurinKey,
            Kind = AgendaItemKind.Task,
            Title = title,
            Description = description,
            Status = status,
            StartUtc = start,
            EndUtc = null,
            IsAllDay = true,
            CreatedByUserKey = author.UserKey,
            Assignments = targets.ToList()
        });
    }

    private void Respond(AgendaItem item, IReadOnlyList<Person> people, double going, double maybe, double notGoing, int daysAgoMin, int daysAgoMax)
    {
        foreach (var person in people)
        {
            var roll = _random.NextDouble();
            AgendaRsvpStatus status;
            if (roll < going)
            {
                status = AgendaRsvpStatus.Going;
            }
            else if (roll < going + maybe)
            {
                status = AgendaRsvpStatus.Maybe;
            }
            else if (roll < going + maybe + notGoing)
            {
                status = AgendaRsvpStatus.NotGoing;
            }
            else
            {
                continue;
            }

            item.Responses.Add(new AgendaResponse
            {
                UserKey = person.UserKey,
                Status = status,
                RespondedAtUtc = DaysAgo(_random.Next(daysAgoMin, daysAgoMax + 1)).AddMinutes(-_random.Next(0, 900))
            });
        }
    }
}
