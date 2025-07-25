using System.Globalization;
using Microsoft.Data.Sqlite;
using NodaTime;
using NodaTime.Text;

namespace StankinAppCore;

public class DatabaseReader : IDataReader
{
    private readonly string _dbPath;

    public DatabaseReader(string dbPath)
    {
        _dbPath = dbPath;
    }

    IEnumerable<string> IDataReader.GetGroups()
    {
        var groups = new List<string>();
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM groups ORDER BY name";

        using var reader = command.ExecuteReader();
        while (reader.Read())
            groups.Add(reader.GetString(0));

        return groups;
    }

    IEnumerable<string> IDataReader.GetRooms()
    {
        var rooms = new List<string>();
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM rooms ORDER BY name";

        using var reader = command.ExecuteReader();
        while (reader.Read())
            rooms.Add(reader.GetString(0));

        return rooms;
    }

    IEnumerable<string> IDataReader.GetTeachers()
    {
        var teachers = new List<string>();
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM teachers ORDER BY name";

        using var reader = command.ExecuteReader();
        while (reader.Read())
            teachers.Add(reader.GetString(0));

        return teachers;
    }

    IEnumerable<Course> IDataReader.GetScheduleForGroupInRange(string groupName, string startDate, string endDate)
    {
        var courses = new List<Course>();
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
                SELECT l.subject, t.name as teacher, l.lesson_type, r.name as room,
                       s.start_time, s.end_time, l.subgroup, g.name as group_name,
                       sd.date
                FROM lessons l
                JOIN sessions s ON l.session_id = s.id
                JOIN groups g ON s.group_id = g.id
                JOIN teachers t ON l.teacher_id = t.id
                LEFT JOIN rooms r ON l.room_id = r.id
                JOIN schedule_dates sd ON l.id = sd.lesson_id
                WHERE g.name = @groupName AND sd.date BETWEEN @startDate AND @endDate
                ORDER BY sd.date, s.start_time";

        command.Parameters.AddWithValue("@groupName", groupName);

        if (!DateTime.TryParseExact(startDate, "yyyy-MM-dd",
             CultureInfo.InvariantCulture, DateTimeStyles.None, out var start))
            throw new ArgumentException("startDate в неверном формате, ожидается yyyy-MM-dd");

        if (!DateTime.TryParseExact(endDate, "yyyy-MM-dd",
             CultureInfo.InvariantCulture, DateTimeStyles.None, out var end))
            throw new ArgumentException("endDate в неверном формате, ожидается yyyy-MM-dd");

        command.Parameters.AddWithValue("@startDate", start.ToString("yyyy-MM-dd"));
        command.Parameters.AddWithValue("@endDate", end.ToString("yyyy-MM-dd"));

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var startTimeStr = reader.GetString(4);
            var endTimeStr = reader.GetString(5);
            var startTime = LocalTimePattern.CreateWithInvariantCulture("HH:mm").Parse(startTimeStr).Value;
            var endTime = LocalTimePattern.CreateWithInvariantCulture("HH:mm").Parse(endTimeStr).Value;
            var duration = Period.Between(startTime, endTime);

            var dateStr = reader.GetString(8);
            if (!DateTime.TryParseExact(dateStr, "yyyy-MM-dd",
                 CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateFromDb))
                throw new ArgumentException("dateFromDb в неверном формате, ожидается yyyy-MM-dd");

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
                Dates = [parsedDate]
            });
        }

        return courses;
    }

    IEnumerable<Course> IDataReader.GetScheduleForGroup(string groupName, string startDate, string endDate)
    {
        var courses = new List<Course>();
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
                SELECT l.subject, t.name as teacher, l.lesson_type, r.name as room,
                       s.start_time, s.end_time, l.subgroup, g.name as group_name,
                       sd.date
                FROM lessons l
                JOIN sessions s ON l.session_id = s.id
                JOIN groups g ON s.group_id = g.id
                JOIN teachers t ON l.teacher_id = t.id
                LEFT JOIN rooms r ON l.room_id = r.id
                JOIN schedule_dates sd ON l.id = sd.lesson_id
                WHERE g.name = @groupName AND sd.date BETWEEN @startDate AND @endDate
                ORDER BY sd.date, s.start_time";

        command.Parameters.AddWithValue("@groupName", groupName);

        if (!DateTime.TryParseExact(startDate, "yyyy-MM-dd",
             CultureInfo.InvariantCulture, DateTimeStyles.None, out var start))
            throw new ArgumentException("startDate в неверном формате, ожидается yyyy-MM-dd");

        if (!DateTime.TryParseExact(endDate, "yyyy-MM-dd",
             CultureInfo.InvariantCulture, DateTimeStyles.None, out var end))
            throw new ArgumentException("endDate в неверном формате, ожидается yyyy-MM-dd");

        command.Parameters.AddWithValue("@startDate", start.ToString("yyyy-MM-dd"));
        command.Parameters.AddWithValue("@endDate", end.ToString("yyyy-MM-dd"));

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var startTimeStr = reader.GetString(4);
            var endTimeStr = reader.GetString(5);
            var startTime = LocalTimePattern.CreateWithInvariantCulture("HH:mm").Parse(startTimeStr).Value;
            var endTime = LocalTimePattern.CreateWithInvariantCulture("HH:mm").Parse(endTimeStr).Value;
            var duration = Period.Between(startTime, endTime);

            var dateStr = reader.GetString(8);
            if (!DateTime.TryParseExact(dateStr, "yyyy-MM-dd",
                 CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateFromDb))
                throw new ArgumentException("dateFromDb в неверном формате, ожидается yyyy-MM-dd");

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
                Dates = [parsedDate]
            });
        }

        return courses;
    }

    IEnumerable<Course> IDataReader.GetScheduleForRoom(string roomName, string startDate, string endDate)
    {
        var courses = new List<Course>();
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
                SELECT l.subject, t.name as teacher, l.lesson_type, r.name as room,
                       s.start_time, s.end_time, l.subgroup, g.name as group_name,
                       sd.date
                FROM lessons l
                JOIN sessions s ON l.session_id = s.id
                JOIN groups g ON s.group_id = g.id
                JOIN teachers t ON l.teacher_id = t.id
                JOIN rooms r ON l.room_id = r.id
                JOIN schedule_dates sd ON l.id = sd.lesson_id
                WHERE r.name = @roomName AND sd.date BETWEEN @startDate AND @endDate
                ORDER BY sd.date, s.start_time";

        command.Parameters.AddWithValue("@roomName", roomName);

        if (!DateTime.TryParseExact(startDate, "yyyy-MM-dd",
             CultureInfo.InvariantCulture, DateTimeStyles.None, out var start))
            throw new ArgumentException("startDate в неверном формате, ожидается yyyy-MM-dd");

        if (!DateTime.TryParseExact(endDate, "yyyy-MM-dd",
             CultureInfo.InvariantCulture, DateTimeStyles.None, out var end))
            throw new ArgumentException("endDate в неверном формате, ожидается yyyy-MM-dd");

        command.Parameters.AddWithValue("@startDate", start.ToString("yyyy-MM-dd"));
        command.Parameters.AddWithValue("@endDate", end.ToString("yyyy-MM-dd"));

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var startTimeStr = reader.GetString(4);
            var endTimeStr = reader.GetString(5);
            var startTime = LocalTimePattern.CreateWithInvariantCulture("HH:mm").Parse(startTimeStr).Value;
            var endTime = LocalTimePattern.CreateWithInvariantCulture("HH:mm").Parse(endTimeStr).Value;
            var duration = Period.Between(startTime, endTime);

            var dateStr = reader.GetString(8);
            if (!DateTime.TryParseExact(dateStr, "yyyy-MM-dd",
                 CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateFromDb))
                throw new ArgumentException("dateFromDb в неверном формате, ожидается yyyy-MM-dd");

            var parsedDate = LocalDate.FromDateTime(dateFromDb);

            courses.Add(new Course
            {
                Subject = reader.GetString(0),
                Teacher = reader.GetString(1),
                Type = reader.GetString(2),
                Cabinet = reader.GetString(3),
                StartTime = startTime,
                Duration = duration,
                Subgroup = reader.IsDBNull(6) ? null : reader.GetString(6),
                GroupName = reader.GetString(7),
                Dates = [parsedDate]
            });
        }

        return courses;
    }

    IEnumerable<Course> IDataReader.GetScheduleForTeacher(string teacherName, string startDate, string endDate)
    {
        var courses = new List<Course>();
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
                SELECT l.subject, t.name as teacher, l.lesson_type, r.name as room,
                       s.start_time, s.end_time, l.subgroup, g.name as group_name,
                       sd.date
                FROM lessons l
                JOIN sessions s ON l.session_id = s.id
                JOIN groups g ON s.group_id = g.id
                JOIN teachers t ON l.teacher_id = t.id
                LEFT JOIN rooms r ON l.room_id = r.id
                JOIN schedule_dates sd ON l.id = sd.lesson_id
                WHERE t.name = @teacherName AND sd.date BETWEEN @startDate AND @endDate
                ORDER BY sd.date, s.start_time";

        command.Parameters.AddWithValue("@teacherName", teacherName);

        if (!DateTime.TryParseExact(startDate, "yyyy-MM-dd",
             CultureInfo.InvariantCulture, DateTimeStyles.None, out var start))
            throw new ArgumentException("startDate в неверном формате, ожидается yyyy-MM-dd");

        if (!DateTime.TryParseExact(endDate, "yyyy-MM-dd",
             CultureInfo.InvariantCulture, DateTimeStyles.None, out var end))
            throw new ArgumentException("endDate в неверном формате, ожидается yyyy-MM-dd");

        command.Parameters.AddWithValue("@startDate", start.ToString("yyyy-MM-dd"));
        command.Parameters.AddWithValue("@endDate", end.ToString("yyyy-MM-dd"));

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var startTimeStr = reader.GetString(4);
            var endTimeStr = reader.GetString(5);
            var startTime = LocalTimePattern.CreateWithInvariantCulture("HH:mm").Parse(startTimeStr).Value;
            var endTime = LocalTimePattern.CreateWithInvariantCulture("HH:mm").Parse(endTimeStr).Value;
            var duration = Period.Between(startTime, endTime);

            var dateStr = reader.GetString(8);
            if (!DateTime.TryParseExact(dateStr, "yyyy-MM-dd",
                 CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateFromDb))
                throw new ArgumentException("dateFromDb в неверном формате, ожидается yyyy-MM-dd");

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
                Dates = [parsedDate]
            });
        }

        return courses;
    }
}