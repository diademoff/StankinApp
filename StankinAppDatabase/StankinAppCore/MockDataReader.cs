using System.Globalization;
using Microsoft.Data.Sqlite;

namespace StankinAppCore;

public class MockDataReader : IDataReader
{
    private readonly SqliteConnection _db;
    private readonly DatabaseReader _reader;

    private static readonly string[] Subjects =
    [
        "Математика", "Программирование", "Физика", "Информатика",
        "Базы данных", "Английский язык", "История", "Схемотехника",
        "Дискретная математика", "Операционные системы"
    ];

    private static readonly string[] GroupNames =
    [
        "ИДБ-22-01", "АДБ-23-04", "АДБ-24-03"
    ];

    private static readonly string[] TeacherNames =
    [
        "Иванов И.И.", "Петров П.П.", "Сидоров С.С.", "Козлов А.Б."
    ];

    private static readonly string[] RoomNames =
    [
        "301", "302", "403", "505"
    ];

    private static readonly (string Start, string End, string Type)[] TimeSlots =
    [
        ("08:30", "10:00", "Лекция"),
        ("10:15", "11:45", "Семинар"),
        ("12:00", "13:30", "Лабораторная работа"),
        ("13:45", "15:15", "Семинар"),
    ];

    // (subjectIdx, teacherIdx, roomIdx, subgroup?)
    // Each group gets the same template but with subject/teacher indices offset by group index.
    // DayOfWeek 1=Monday ... 5=Friday. Each weekday has up to 4 slots matching TimeSlots.
    private static readonly (int subj, int teach, int room, string? subg)[][] ScheduleTemplate =
    [
        /* Пн */ [(0,0,0,null), (1,1,1,null), (2,2,2,"1"), (3,3,3,null)],
        /* Вт */ [(4,3,2,null), (5,2,1,null), (6,1,0,"1"), (7,0,3,null)],
        /* Ср */ [(8,0,1,null), (9,1,2,"1"), (0,2,3,null)],
        /* Чт */ [(1,3,0,null), (3,0,1,null), (5,1,2,"1"), (7,2,3,null)],
        /* Пт */ [(2,0,1,null), (6,1,2,"1"), (9,2,3,null)],
    ];

    public MockDataReader()
    {
        _db = new SqliteConnection("Data Source=:memory:");
        _db.Open();
        CreateSchema();
        Seed();
        _reader = new DatabaseReader(_db);
    }

    public IEnumerable<string> GetGroups()    => _reader.GetGroups();
    public IEnumerable<string> GetRooms()     => _reader.GetRooms();
    public IEnumerable<string> GetTeachers()  => _reader.GetTeachers();
    public IEnumerable<Course> GetScheduleForGroup(string groupName, string startDate, string endDate)
        => _reader.GetScheduleForGroup(groupName, startDate, endDate);
    public IEnumerable<Course> GetScheduleForRoom(string roomName, string startDate, string endDate)
        => _reader.GetScheduleForRoom(roomName, startDate, endDate);
    public IEnumerable<Course> GetScheduleForTeacher(string teacherName, string startDate, string endDate)
        => _reader.GetScheduleForTeacher(teacherName, startDate, endDate);

    private void CreateSchema()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE groups (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL UNIQUE);
            CREATE TABLE teachers (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL UNIQUE);
            CREATE TABLE rooms (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL UNIQUE);
            CREATE TABLE sessions (id INTEGER PRIMARY KEY AUTOINCREMENT, group_id INTEGER NOT NULL, start_time TEXT NOT NULL, end_time TEXT NOT NULL);
            CREATE TABLE lessons (id INTEGER PRIMARY KEY AUTOINCREMENT, session_id INTEGER NOT NULL, subject TEXT NOT NULL, teacher_id INTEGER NOT NULL, lesson_type TEXT NOT NULL, room_id INTEGER, subgroup TEXT);
            CREATE TABLE schedule_dates (id INTEGER PRIMARY KEY AUTOINCREMENT, lesson_id INTEGER NOT NULL, date TEXT NOT NULL, sequence_position INTEGER NOT NULL, sequence_length INTEGER NOT NULL);";
        cmd.ExecuteNonQuery();
    }

    private void Seed()
    {
        foreach (var name in GroupNames)   Insert("groups", "name", name);
        foreach (var name in TeacherNames)  Insert("teachers", "name", name);
        foreach (var name in RoomNames)     Insert("rooms", "name", name);

        var today = DateTime.Today;
        var start = today.AddDays(-30);
        var end = today.AddDays(30);

        for (int gi = 0; gi < GroupNames.Length; gi++)
        {
            var groupId = gi + 1;

            for (var d = start; d <= end; d = d.AddDays(1))
            {
                int dow = (int)d.DayOfWeek;
                if (dow == 0 || dow == 6) continue; // skip weekends
                int dayIdx = dow - 1; // 0=Mon ... 4=Fri

                var dateStr = d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                var daySlots = ScheduleTemplate[dayIdx];

                for (int si = 0; si < daySlots.Length && si < TimeSlots.Length; si++)
                {
                    var (subjTmpl, teachTmpl, roomTmpl, subg) = daySlots[si];
                    var slot = TimeSlots[si];

                    var subjIdx  = (subjTmpl + gi)  % Subjects.Length;
                    var teachIdx = (teachTmpl + gi) % TeacherNames.Length;
                    var roomIdx  = roomTmpl % RoomNames.Length;
                    var teacherId = teachIdx + 1;
                    var roomId    = roomIdx + 1;

                    long sessionId = ExecuteScalar(@"
                        INSERT INTO sessions (group_id, start_time, end_time)
                        VALUES (@g, @s, @e);
                        SELECT last_insert_rowid();",
                        ("@g", groupId), ("@s", slot.Start), ("@e", slot.End));

                    long lessonId = ExecuteScalar(@"
                        INSERT INTO lessons (session_id, subject, teacher_id, lesson_type, room_id, subgroup)
                        VALUES (@s, @subj, @t, @type, @r, @sub);
                        SELECT last_insert_rowid();",
                        ("@s", sessionId), ("@subj", Subjects[subjIdx]), ("@t", teacherId),
                        ("@type", slot.Type), ("@r", roomId), ("@sub", subg ?? (object)DBNull.Value));

                    ExecuteNonQuery(@"
                        INSERT INTO schedule_dates (lesson_id, date, sequence_position, sequence_length)
                        VALUES (@l, @d, 0, 0);",
                        ("@l", lessonId), ("@d", dateStr));
                }
            }
        }

        ComputeSequences();
    }

    /// <summary>
    /// Вычисляет sequence_position и sequence_length для каждого schedule_dates
    /// по логике DatabaseBuilder: группировка по (group, subject, teacher, type, subgroup),
    /// сортировка дат внутри группы, позиция = индекс даты + 1.
    /// </summary>
    private void ComputeSequences()
    {
        var entries = new List<Entry>();
        using (var cmd = _db.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT sd.id, g.id, l.subject, t.id, l.lesson_type, COALESCE(l.subgroup,''), sd.date
                FROM schedule_dates sd
                JOIN lessons l ON sd.lesson_id = l.id
                JOIN sessions s ON l.session_id = s.id
                JOIN groups g ON s.group_id = g.id
                JOIN teachers t ON l.teacher_id = t.id";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                entries.Add(new Entry(r.GetInt64(0), r.GetInt64(1),
                    r.GetString(2), r.GetInt64(3), r.GetString(4), r.GetString(5), r.GetString(6)));
        }

        var groups = entries.GroupBy(e => (e.GroupId, e.Subject, e.TeacherId, e.Type, e.Subgroup));

        using var upd = _db.CreateCommand();
        upd.CommandText = "UPDATE schedule_dates SET sequence_position = @p, sequence_length = @l WHERE id = @i";
        var pPos = upd.Parameters.Add("@p", SqliteType.Integer);
        var pLen = upd.Parameters.Add("@l", SqliteType.Integer);
        var pId  = upd.Parameters.Add("@i", SqliteType.Integer);

        foreach (var grp in groups)
        {
            var sorted = grp.OrderBy(e => e.Date).ToList();
            int total = sorted.Count;
            for (int i = 0; i < sorted.Count; i++)
            {
                pPos.Value = i + 1;
                pLen.Value = total;
                pId.Value  = sorted[i].DateId;
                upd.ExecuteNonQuery();
            }
        }
    }

    private sealed record Entry(long DateId, long GroupId, string Subject, long TeacherId, string Type, string Subgroup, string Date);

    private void Insert(string table, string col, string value) =>
        ExecuteNonQuery($"INSERT INTO {table} ({col}) VALUES (@v);", ("@v", value));

    private long ExecuteScalar(string sql, params (string, object)[] parameters)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in parameters)
            cmd.Parameters.AddWithValue(name, value);
        return (long)(cmd.ExecuteScalar() ?? throw new InvalidOperationException("ExecuteScalar returned null"));
    }

    private void ExecuteNonQuery(string sql, params (string, object)[] parameters)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in parameters)
            cmd.Parameters.AddWithValue(name, value);
        cmd.ExecuteNonQuery();
    }
}
