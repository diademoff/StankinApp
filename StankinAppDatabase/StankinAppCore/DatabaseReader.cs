using System.Globalization;
using Microsoft.Data.Sqlite;
using NodaTime;
using NodaTime.Text;

namespace StankinAppCore;

public class DatabaseReader(string dbPath) : IDataReader
{
    private const string DateFormat = "yyyy-MM-dd";
    private const string TimeFormat = "HH:mm";

    private SqliteConnection GetOpenConnection()
    {
        var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();
        return connection;
    }

    private List<string> GetListFromTable(string tableName)
    {
        var items = new List<string>();
        using var connection = GetOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT name FROM {tableName} ORDER BY name";
        using var reader = command.ExecuteReader();
        while (reader.Read())
            items.Add(reader.GetString(0));
        return items;
    }

    public IEnumerable<string> GetGroups() => GetListFromTable("groups");
    public IEnumerable<string> GetRooms() => GetListFromTable("rooms");
    public IEnumerable<string> GetTeachers() => GetListFromTable("teachers");

    private List<Course> GetSchedule(string sql, params SqliteParameter[] parameters)
    {
        var courses = new List<Course>();
        using var connection = GetOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddRange(parameters);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var startTimeStr = reader.GetString(4);
            var endTimeStr = reader.GetString(5);
            var startTime = LocalTimePattern.CreateWithInvariantCulture(TimeFormat).Parse(startTimeStr).Value;
            var endTime = LocalTimePattern.CreateWithInvariantCulture(TimeFormat).Parse(endTimeStr).Value;
            var duration = Period.Between(startTime, endTime);

            var dateStr = reader.GetString(8);
            if (!DateTime.TryParseExact(dateStr, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateFromDb))
                throw new ArgumentException($"dateFromDb в неверном формате, ожидается {DateFormat}");

            var parsedDate = LocalDate.FromDateTime(dateFromDb);

            courses.Add(new Course
            {
                Subject = reader.GetString(0),
                Teacher = reader.GetString(1),
                Type = reader.GetString(2),
                Cabinet = reader.IsDBNull(3) ? null : reader.GetString(3),
                StartTime = startTime,
                Duration = duration,
                Subgroup = reader.IsDBNull(6) ? null : reader.GetString(6),
                GroupName = reader.GetString(7),
                Dates = [parsedDate],
                SequencePosition = reader.GetInt32(9),
                SequenceLength = reader.GetInt32(10)
            });
        }
        return courses;
    }

    public IEnumerable<Course> GetScheduleForGroup(string groupName, string startDate, string endDate)
    {
        if (!DateTime.TryParseExact(startDate, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var start))
            throw new ArgumentException($"startDate в неверном формате, ожидается {DateFormat}");

        if (!DateTime.TryParseExact(endDate, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var end))
            throw new ArgumentException($"endDate в неверном формате, ожидается {DateFormat}");

        var sql = @"
            SELECT l.subject, t.name as teacher, l.lesson_type, r.name as room,
                   s.start_time, s.end_time, l.subgroup, g.name as group_name,
                   sd.date, sd.sequence_position, sd.sequence_length
            FROM lessons l
            JOIN sessions s ON l.session_id = s.id
            JOIN groups g ON s.group_id = g.id
            JOIN teachers t ON l.teacher_id = t.id
            LEFT JOIN rooms r ON l.room_id = r.id
            JOIN schedule_dates sd ON l.id = sd.lesson_id
            WHERE g.name = @groupName AND sd.date BETWEEN @startDate AND @endDate
            ORDER BY sd.date, s.start_time";

        var parameters = new[]
        {
            new SqliteParameter("@groupName", groupName),
            new SqliteParameter("@startDate", start.ToString(DateFormat)),
            new SqliteParameter("@endDate", end.ToString(DateFormat))
        };

        return GetSchedule(sql, parameters);
    }

    public IEnumerable<Course> GetScheduleForRoom(string roomName, string startDate, string endDate)
    {
        if (!DateTime.TryParseExact(startDate, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var start))
            throw new ArgumentException($"startDate в неверном формате, ожидается {DateFormat}");

        if (!DateTime.TryParseExact(endDate, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var end))
            throw new ArgumentException($"endDate в неверном формате, ожидается {DateFormat}");

        var sql = @"
            SELECT l.subject, t.name as teacher, l.lesson_type, r.name as room,
                   s.start_time, s.end_time, l.subgroup, g.name as group_name,
                   sd.date, sd.sequence_position, sd.sequence_length
            FROM lessons l
            JOIN sessions s ON l.session_id = s.id
            JOIN groups g ON s.group_id = g.id
            JOIN teachers t ON l.teacher_id = t.id
            JOIN rooms r ON l.room_id = r.id
            JOIN schedule_dates sd ON l.id = sd.lesson_id
            WHERE r.name = @roomName AND sd.date BETWEEN @startDate AND @endDate
            ORDER BY sd.date, s.start_time";

        var parameters = new[]
        {
            new SqliteParameter("@roomName", roomName),
            new SqliteParameter("@startDate", start.ToString(DateFormat)),
            new SqliteParameter("@endDate", end.ToString(DateFormat))
        };

        return GetSchedule(sql, parameters);
    }

    public IEnumerable<Course> GetScheduleForTeacher(string teacherName, string startDate, string endDate)
    {
        if (!DateTime.TryParseExact(startDate, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var start))
            throw new ArgumentException($"startDate в неверном формате, ожидается {DateFormat}");

        if (!DateTime.TryParseExact(endDate, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var end))
            throw new ArgumentException($"endDate в неверном формате, ожидается {DateFormat}");

        var sql = @"
            SELECT l.subject, t.name as teacher, l.lesson_type, r.name as room,
                   s.start_time, s.end_time, l.subgroup, g.name as group_name,
                   sd.date, sd.sequence_position, sd.sequence_length
            FROM lessons l
            JOIN sessions s ON l.session_id = s.id
            JOIN groups g ON s.group_id = g.id
            JOIN teachers t ON l.teacher_id = t.id
            LEFT JOIN rooms r ON l.room_id = r.id
            JOIN schedule_dates sd ON l.id = sd.lesson_id
            WHERE t.name = @teacherName AND sd.date BETWEEN @startDate AND @endDate
            ORDER BY sd.date, s.start_time";

        var parameters = new[]
        {
            new SqliteParameter("@teacherName", teacherName),
            new SqliteParameter("@startDate", start.ToString(DateFormat)),
            new SqliteParameter("@endDate", end.ToString(DateFormat))
        };

        return GetSchedule(sql, parameters);
    }
}