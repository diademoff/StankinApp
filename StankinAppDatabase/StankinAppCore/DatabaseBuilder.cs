namespace StankinAppCore;

using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;

public class DatabaseBuilder
{
    private readonly string _dbPath;

    public DatabaseBuilder(string dbPath)
    {
        _dbPath = dbPath;
    }

    public void CreateSchema()
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();

        using var command = connection.CreateCommand();

        // groups table
        command.CommandText = @"
                CREATE TABLE IF NOT EXISTS groups (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    name TEXT NOT NULL UNIQUE
                );";
        command.ExecuteNonQuery();

        // teachers table
        command.CommandText = @"
                CREATE TABLE IF NOT EXISTS teachers (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    name TEXT NOT NULL UNIQUE
                );";
        command.ExecuteNonQuery();

        // rooms table
        command.CommandText = @"
                CREATE TABLE IF NOT EXISTS rooms (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    name TEXT NOT NULL UNIQUE
                );";
        command.ExecuteNonQuery();

        // sessions table
        command.CommandText = @"
                CREATE TABLE IF NOT EXISTS sessions (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    group_id INTEGER NOT NULL,
                    start_time TEXT NOT NULL,
                    end_time TEXT NOT NULL,
                    FOREIGN KEY (group_id) REFERENCES groups(id)
                );";
        command.ExecuteNonQuery();

        // lessons table
        command.CommandText = @"
                CREATE TABLE IF NOT EXISTS lessons (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    session_id INTEGER NOT NULL,
                    subject TEXT NOT NULL,
                    teacher_id INTEGER NOT NULL,
                    lesson_type TEXT NOT NULL,
                    room_id INTEGER,
                    subgroup TEXT,
                    FOREIGN KEY (session_id) REFERENCES sessions(id),
                    FOREIGN KEY (teacher_id) REFERENCES teachers(id),
                    FOREIGN KEY (room_id) REFERENCES rooms(id)
                );";
        command.ExecuteNonQuery();

        // schedule_dates table
        command.CommandText = @"
                CREATE TABLE IF NOT EXISTS schedule_dates (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    lesson_id INTEGER NOT NULL,
                    date TEXT NOT NULL,
                    sequence_position INTEGER NOT NULL,
                    sequence_length INTEGER NOT NULL,
                    FOREIGN KEY (lesson_id) REFERENCES lessons(id)
                );";
        command.ExecuteNonQuery();
    }

    public void InsertGroupSchedule(string groupName, List<Course> courses, int currentYear)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();

        var groupId = GetOrCreate(connection, "groups", "name", groupName);

        var teachers = courses.Select(c => c.Teacher).Distinct().ToList();
        var rooms = courses.Select(c => c.Cabinet).Where(c => !string.IsNullOrEmpty(c)).Distinct().ToList();

        var teacherIds = new Dictionary<string, long>();
        foreach (var teacher in teachers)
            if (teacher is not null)
                teacherIds[teacher] = GetOrCreate(connection, "teachers", "name", teacher);

        var roomIds = new Dictionary<string, long>();
        foreach (var room in rooms)
            if (room is not null)
                roomIds[room] = GetOrCreate(connection, "rooms", "name", room);

        // Группируем курсы по (subject, teacher, lesson_type, subgroup)
        // и считаем глобальное число дат для каждой группы
        var courseKey = (Course c) => (c.Subject, c.Teacher, c.Type, c.Subgroup);

        var globalSequenceLengths = courses
            .GroupBy(courseKey)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(c => c.Dates?.Count ?? 0)
            );

        // Счётчик уже вставленных дат для каждой группы курсов
        var globalSequenceCounters = courses
            .GroupBy(courseKey)
            .ToDictionary(g => g.Key, _ => 0);

        foreach (var course in courses)
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
                    INSERT INTO sessions (group_id, start_time, end_time)
                    VALUES (@group_id, @start_time, @end_time);
                    SELECT last_insert_rowid();";

            command.Parameters.AddWithValue("@group_id", groupId);
            command.Parameters.AddWithValue("@start_time", $"{course.StartTime.Hour:D2}:{course.StartTime.Minute:D2}");
            command.Parameters.AddWithValue("@end_time", $"{(course.StartTime + course.Duration).Hour:D2}:{(course.StartTime + course.Duration).Minute:D2}");

            var sessionId = (long)(command.ExecuteScalar() ?? throw new NullReferenceException("sessionId is null"));

            var teacherId = teacherIds[course.Teacher ?? throw new NullReferenceException("course.Teacher is null")];
            var roomId = !string.IsNullOrEmpty(course.Cabinet) ? roomIds[course.Cabinet] : (long?)null;

            command.CommandText = @"
                    INSERT INTO lessons (session_id, subject, teacher_id, lesson_type, room_id, subgroup)
                    VALUES (@session_id, @subject, @teacher_id, @lesson_type, @room_id, @subgroup);
                    SELECT last_insert_rowid();";
            command.Parameters.Clear();
            command.Parameters.AddWithValue("@session_id", sessionId);
            command.Parameters.AddWithValue("@subject", course.Subject);
            command.Parameters.AddWithValue("@teacher_id", teacherId);
            command.Parameters.AddWithValue("@lesson_type", course.Type);
            command.Parameters.AddWithValue("@room_id", roomId ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@subgroup", course.Subgroup ?? (object)DBNull.Value);

            var lessonId = (long)(command.ExecuteScalar() ?? throw new NullReferenceException("lessonId is null"));

            var key = courseKey(course);
            var totalLength = globalSequenceLengths[key];

            for (int i = 0; i < course.Dates?.Count; i++)
            {
                globalSequenceCounters[key]++;          // глобальная позиция
                var globalPosition = globalSequenceCounters[key];

                var date = course.Dates[i];
                command.CommandText = @"
                        INSERT INTO schedule_dates (lesson_id, date, sequence_position, sequence_length)
                        VALUES (@lesson_id, @date, @sequence_position, @sequence_length)";
                command.Parameters.Clear();
                command.Parameters.AddWithValue("@lesson_id", lessonId);
                var dateParam = command.Parameters.Add("@date", SqliteType.Text);
                dateParam.Value = new DateTime(currentYear, date.Month, date.Day).ToString("yyyy-MM-dd");
                command.Parameters.AddWithValue("@sequence_position", globalPosition);
                command.Parameters.AddWithValue("@sequence_length", totalLength);
                command.ExecuteNonQuery();
            }
        }
    }

    private long GetOrCreate(SqliteConnection connection, string table, string uniqueField, string value)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT id FROM {table} WHERE {uniqueField} = @value";
        command.Parameters.AddWithValue("@value", value);

        var result = command.ExecuteScalar();
        if (result != null)
            return (long)result;

        command.CommandText = $"INSERT INTO {table} ({uniqueField}) VALUES (@value); SELECT last_insert_rowid();";
        return (long)(command.ExecuteScalar() ?? throw new NullReferenceException("GetOrCreate command is null"));
    }
}