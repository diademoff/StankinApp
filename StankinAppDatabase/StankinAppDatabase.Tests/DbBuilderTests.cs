using NUnit.Framework;
using StankinAppCore;
using Microsoft.Data.Sqlite;
using NodaTime;

namespace DatabaseBuilderTests;

[TestFixture]
public class DbBuilderTests
{
    private string _dbPath = null!;
    private DatabaseBuilder _db = null!;

    [SetUp]
    public void SetUp()
    {
        // Временный файл для каждого теста — изоляция гарантирована
        _dbPath = Path.GetTempFileName();
        _db = new DatabaseBuilder(_dbPath);
        _db.CreateSchema();
    }

    [TearDown]
    public void TearDown()
    {
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    // Основной сценарий: Алгебра Пн×3 + Алгебра Ср×1 → должно быть 1/4, 2/4, 3/4, 4/4
    [Test]
    public void InsertGroupSchedule_ShouldGroupSameCoursesAndCorrectSequences()
    {
        var teacher = "Иванов И.И.";
        var subject = "Алгебра";
        var type = "Лекция";

        var monday = new Course
        {
            Subject = subject,
            Type = type,
            Teacher = teacher,
            StartTime = new LocalTime(9, 0),
            Duration = Period.FromHours(2),
            Dates = new List<LocalDate>
            {
                new LocalDate(2024, 9, 2),  // Пн1
                new LocalDate(2024, 9, 9),  // Пн2
                new LocalDate(2024, 9, 16), // Пн3
            }
        };

        var wednesday = new Course
        {
            Subject = subject,
            Type = type,
            Teacher = teacher,
            StartTime = new LocalTime(9, 0),
            Duration = Period.FromHours(2),
            Dates = new List<LocalDate>
            {
                new LocalDate(2024, 9, 4), // Ср1
            }
        };

        _db.InsertGroupSchedule("ПИ-101", new List<Course> { monday, wednesday }, 2024);

        var rows = ReadScheduleDates(_dbPath);

        // Всего 4 записи
        Assert.That(rows, Has.Count.EqualTo(4));

        // Все записи имеют sequence_length = 4 (сумарно 4 занятия по Алгебре)
        Assert.That(rows.All(r => r.SequenceLength == 4), Is.True,
            "sequence_length должен быть 4 для всех занятий");

        // Позиции идут 1..4 без дублей
        var positions = rows.Select(r => r.SequencePosition).OrderBy(x => x).ToList();
        Assert.That(positions, Is.EqualTo(new[] { 1, 2, 3, 4 }));

        // Среда (4 сент) — это позиция 2 (хронологически: Пн1=1, Ср=2, Пн2=3, Пн3=4)
        var wed = rows.First(r => r.Date == "2024-09-04");
        Assert.That(wed.SequencePosition, Is.EqualTo(2));
    }

    // Разные предметы не должны влиять друг на друга
    [Test]
    public void InsertGroupSchedule_DifferentSubjects_SeparateSequences()
    {
        var algebra = new Course
        {
            Subject = "Алгебра", Type = "Лекция", Teacher = "Иванов И.И.",
            StartTime = new LocalTime(9, 0), Duration = Period.FromHours(2),
            Dates = new List<LocalDate> { new LocalDate(2024, 9, 2) }
        };

        var physics = new Course
        {
            Subject = "Физика", Type = "Лекция", Teacher = "Петров П.П.",
            StartTime = new LocalTime(11, 0), Duration = Period.FromHours(2),
            Dates = new List<LocalDate> { new LocalDate(2024, 9, 2) }
        };

        _db.InsertGroupSchedule("ПИ-101", new List<Course> { algebra, physics }, 2024);

        var rows = ReadScheduleDates(_dbPath);
        Assert.That(rows, Has.Count.EqualTo(2));
        // Каждый предмет — своя последовательность 1/1
        Assert.That(rows.All(r => r.SequencePosition == 1 && r.SequenceLength == 1), Is.True);
    }

    // Разные подгруппы — разные последовательности
    [Test]
    public void InsertGroupSchedule_DifferentSubgroups_SeparateSequences()
    {
        var sub1 = new Course
        {
            Subject = "Алгебра", Type = "Семинар", Teacher = "Иванов И.И.",
            Subgroup = "1",
            StartTime = new LocalTime(9, 0), Duration = Period.FromHours(2),
            Dates = new List<LocalDate>
            {
                new LocalDate(2024, 9, 2),
                new LocalDate(2024, 9, 9),
            }
        };

        var sub2 = new Course
        {
            Subject = "Алгебра", Type = "Семинар", Teacher = "Иванов И.И.",
            Subgroup = "2",
            StartTime = new LocalTime(9, 0), Duration = Period.FromHours(2),
            Dates = new List<LocalDate>
            {
                new LocalDate(2024, 9, 4),
            }
        };

        _db.InsertGroupSchedule("ПИ-101", new List<Course> { sub1, sub2 }, 2024);

        var rows = ReadScheduleDates(_dbPath);
        Assert.That(rows, Has.Count.EqualTo(3));

        // Подгруппа 2: только 1 занятие → 1/1
        var sub2Rows = rows.Where(r => r.Date == "2024-09-04").ToList();
        Assert.That(sub2Rows.Single().SequenceLength, Is.EqualTo(1));

        // Подгруппа 1: 2 занятия → 1/2, 2/2
        var sub1Rows = rows.Where(r => r.Date != "2024-09-04").OrderBy(r => r.Date).ToList();
        Assert.That(sub1Rows[0].SequencePosition, Is.EqualTo(1));
        Assert.That(sub1Rows[1].SequencePosition, Is.EqualTo(2));
        Assert.That(sub1Rows.All(r => r.SequenceLength == 2), Is.True);
    }

    // Разные типы занятий (Лекция / Семинар) — разные последовательности
    [Test]
    public void InsertGroupSchedule_DifferentLessonTypes_SeparateSequences()
    {
        var lecture = new Course
        {
            Subject = "Алгебра", Type = "Лекция", Teacher = "Иванов И.И.",
            StartTime = new LocalTime(9, 0), Duration = Period.FromHours(2),
            Dates = new List<LocalDate> { new LocalDate(2024, 9, 2) }
        };

        var seminar = new Course
        {
            Subject = "Алгебра", Type = "Семинар", Teacher = "Иванов И.И.",
            StartTime = new LocalTime(11, 0), Duration = Period.FromHours(2),
            Dates = new List<LocalDate>
            {
                new LocalDate(2024, 9, 2),
                new LocalDate(2024, 9, 9),
            }
        };

        _db.InsertGroupSchedule("ПИ-101", new List<Course> { lecture, seminar }, 2024);

        var rows = ReadScheduleDates(_dbPath);
        Assert.That(rows, Has.Count.EqualTo(3));

        // Лекция: 1 занятие → sequence_length = 1
        var lectureRows = rows.Where(r => r.SequenceLength == 1).ToList();
        Assert.That(lectureRows, Has.Count.EqualTo(1));
        Assert.That(lectureRows[0].SequencePosition, Is.EqualTo(1));

        // Семинар: 2 занятия → sequence_length = 2, позиции 1 и 2
        var seminarRows = rows.Where(r => r.SequenceLength == 2).OrderBy(r => r.Date).ToList();
        Assert.That(seminarRows, Has.Count.EqualTo(2));
        Assert.That(seminarRows[0].SequencePosition, Is.EqualTo(1));
        Assert.That(seminarRows[1].SequencePosition, Is.EqualTo(2));
    }

    // --- Хелпер для чтения schedule_dates напрямую из БД ---
    private static List<ScheduleDateRow> ReadScheduleDates(string dbPath)
    {
        var result = new List<ScheduleDateRow>();
        using var conn = new SqliteConnection($"Data Source={dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT lesson_id, date, sequence_position, sequence_length FROM schedule_dates";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new ScheduleDateRow(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetInt32(2),
                reader.GetInt32(3)
            ));
        }
        return result;
    }

    [Test]
    public void InsertGroupSchedule_SameSubjectSameDay_ShouldHaveSameSequencePosition()
    {
        // Две пары в один нумеруются как одна пара
        // Делается так, потому что лабораторные работы это 2 пары
        // Arrange
        var dates = new List<LocalDate>
        {
            new LocalDate(2024, 9, 2), // Пн 1
            new LocalDate(2024, 9, 9)  // Пн 2
        };

        var courses = new List<Course>
        {
            // Первая пара в понедельник
            new Course
            {
                Subject = "Алгебра",
                Teacher = "Иванов И.И.",
                Type = "лекции",
                StartTime = new LocalTime(9, 0),
                Duration = Period.FromMinutes(90),
                Dates = dates
            },
            // Вторая пара в ТОТ ЖЕ понедельник
            new Course
            {
                Subject = "Алгебра",
                Teacher = "Иванов И.И.",
                Type = "лекции",
                StartTime = new LocalTime(10, 45),
                Duration = Period.FromMinutes(90),
                Dates = dates
            }
        };

        // Act
        _db.InsertGroupSchedule("ПИ-101", courses, 2024);

        // Assert
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT lesson_id, date, sequence_position, sequence_length FROM schedule_dates";

        using var command = conn.CreateCommand();
        command.CommandText = @"
            SELECT sd.date, sd.sequence_position, sd.sequence_length, s.start_time
            FROM schedule_dates sd
            JOIN lessons l ON sd.lesson_id = l.id
            JOIN sessions s ON l.session_id = s.id
            WHERE l.subject = 'Алгебра'
            ORDER BY sd.date, s.start_time";

        using var reader = command.ExecuteReader();
        var results = new List<(string Date, int Pos, int Len, string StartTime)>();

        while (reader.Read())
        {
            results.Add((
                reader.GetString(0),
                reader.GetInt32(1),
                reader.GetInt32(2),
                reader.GetString(3)
            ));
        }

        Assert.That(results, Has.Count.EqualTo(4), "Должно быть 4 записи (2 дня по 2 пары)");

        Assert.Multiple(() =>
        {
            // Пн 1 - Первая пара
            Assert.That(results[0].Date, Is.EqualTo("2024-09-02"));
            Assert.That(results[0].StartTime, Is.EqualTo("09:00"));
            Assert.That(results[0].Pos, Is.EqualTo(1));
            Assert.That(results[0].Len, Is.EqualTo(2)); // Всего 2 уникальных дня

            // Пн 1 - Вторая пара (та же позиция!)
            Assert.That(results[1].Date, Is.EqualTo("2024-09-02"));
            Assert.That(results[1].StartTime, Is.EqualTo("10:45"));
            Assert.That(results[1].Pos, Is.EqualTo(1));
            Assert.That(results[1].Len, Is.EqualTo(2));

            // Пн 2 - Первая пара
            Assert.That(results[2].Date, Is.EqualTo("2024-09-09"));
            Assert.That(results[2].StartTime, Is.EqualTo("09:00"));
            Assert.That(results[2].Pos, Is.EqualTo(2)); // Индекс вырос, так как день поменялся
            Assert.That(results[2].Len, Is.EqualTo(2));

            // Пн 2 - Вторая пара
            Assert.That(results[3].Date, Is.EqualTo("2024-09-09"));
            Assert.That(results[3].StartTime, Is.EqualTo("10:45"));
            Assert.That(results[3].Pos, Is.EqualTo(2));
            Assert.That(results[3].Len, Is.EqualTo(2));
        });
    }

    private record ScheduleDateRow(long LessonId, string Date, int SequencePosition, int SequenceLength);
}