using Microsoft.Data.Sqlite;
using NodaTime;
using NodaTime.Text;

namespace StankinAppCore;

public class DatabaseReader : IDataReader
{
    private const string DateFormat = "yyyy-MM-dd";
    private const string TimeFormat = "HH:mm";

    private const string SelectScheduleSql = @"
        SELECT l.subject, t.name as teacher, l.lesson_type, r.name as room,
               s.start_time, s.end_time, l.subgroup, g.name as group_name,
               sd.date, sd.sequence_position, sd.sequence_length
        FROM lessons l
        JOIN sessions s ON l.session_id = s.id
        JOIN groups g ON s.group_id = g.id
        JOIN teachers t ON l.teacher_id = t.id
        LEFT JOIN rooms r ON l.room_id = r.id
        JOIN schedule_dates sd ON l.id = sd.lesson_id";

    private readonly string? _dbPath;
    private readonly SqliteConnection? _sharedConnection;

    public DatabaseReader(string dbPath)
    {
        _dbPath = dbPath;
    }

    public DatabaseReader(SqliteConnection sharedConnection)
    {
        _sharedConnection = sharedConnection;
    }

    private SqliteConnection GetOpenConnection()
    {
        if (_sharedConnection != null)
            return _sharedConnection;
        var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();
        return connection;
    }

    private List<string> GetListFromTable(string tableName)
    {
        var items = new List<string>();
        var connection = GetOpenConnection();
        var ownsConnection = _sharedConnection == null;
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT name FROM {tableName} ORDER BY name";
            using var reader = command.ExecuteReader();
            while (reader.Read())
                items.Add(reader.GetString(0));
        }
        finally
        {
            if (ownsConnection)
                connection.Dispose();
        }
        return items;
    }

    public IEnumerable<string> GetGroups() => GetListFromTable("groups");
    public IEnumerable<string> GetRooms() => GetListFromTable("rooms");
    public IEnumerable<string> GetTeachers() => GetListFromTable("teachers");

    private static string FormatDate(string date)
    {
        var result = LocalDatePattern.CreateWithInvariantCulture(DateFormat).Parse(date);
        if (!result.Success)
            throw new ArgumentException($"Дата в неверном формате, ожидается {DateFormat}");
        return date;
    }

    private List<Course> GetSchedule(string sql, params SqliteParameter[] parameters)
    {
        var courses = new List<Course>();
        var connection = GetOpenConnection();
        var ownsConnection = _sharedConnection == null;
        try
        {
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
                var dateResult = LocalDatePattern.CreateWithInvariantCulture(DateFormat).Parse(dateStr);
                if (!dateResult.Success)
                    throw new ArgumentException($"dateFromDb в неверном формате, ожидается {DateFormat}");
                var parsedDate = dateResult.Value;

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
        }
        finally
        {
            if (ownsConnection)
                connection.Dispose();
        }
        return courses;
    }

    private IEnumerable<Course> GetScheduleFor(string whereClause, SqliteParameter[] parameters)
    {
        var sql = $"{SelectScheduleSql} WHERE {whereClause} ORDER BY sd.date, s.start_time";
        return GetSchedule(sql, parameters);
    }

    public IEnumerable<Course> GetScheduleForGroup(string groupName, string startDate, string endDate)
    {
        var parameters = new[]
        {
            new SqliteParameter("@groupName", groupName),
            new SqliteParameter("@startDate", FormatDate(startDate)),
            new SqliteParameter("@endDate", FormatDate(endDate))
        };
        return GetScheduleFor("g.name = @groupName AND sd.date BETWEEN @startDate AND @endDate", parameters);
    }

    public IEnumerable<Course> GetScheduleForRoom(string roomName, string startDate, string endDate)
    {
        var parameters = new[]
        {
            new SqliteParameter("@roomName", roomName),
            new SqliteParameter("@startDate", FormatDate(startDate)),
            new SqliteParameter("@endDate", FormatDate(endDate))
        };
        return GetScheduleFor("r.name = @roomName AND sd.date BETWEEN @startDate AND @endDate", parameters);
    }

    public IEnumerable<Course> GetScheduleForTeacher(string teacherName, string startDate, string endDate)
    {
        var parameters = new[]
        {
            new SqliteParameter("@teacherName", teacherName),
            new SqliteParameter("@startDate", FormatDate(startDate)),
            new SqliteParameter("@endDate", FormatDate(endDate))
        };
        return GetScheduleFor("t.name = @teacherName AND sd.date BETWEEN @startDate AND @endDate", parameters);
    }

    public IEnumerable<Course> GetScheduleBySubject(string subjectName, string teacherName, string groupName, string startDate, string endDate)
    {
        var parameters = new[]
        {
            new SqliteParameter("@subjectName", subjectName),
            new SqliteParameter("@teacherName", teacherName),
            new SqliteParameter("@groupName", groupName),
            new SqliteParameter("@startDate", FormatDate(startDate)),
            new SqliteParameter("@endDate", FormatDate(endDate))
        };
        return GetScheduleFor(
            "l.subject = @subjectName AND t.name = @teacherName AND g.name = @groupName AND sd.date BETWEEN @startDate AND @endDate",
            parameters);
    }
}
