using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using NodaTime;
using NodaTime.Text;
using System.Text.RegularExpressions;

namespace StankinAppDatabase
{
    public class DatabaseBuilder
    {
        private readonly string _dbPath;
        private readonly ScheduleJsonReader _scheduleReader;

        public DatabaseBuilder(int currentYear, string dbPath = "schedule.db")
        {
            _dbPath = dbPath;
            _scheduleReader = new ScheduleJsonReader(currentYear);
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

            // schedule_dates table, year �� �����, ������ ������ dd.MM
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS schedule_dates (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    lesson_id INTEGER NOT NULL,
                    date TEXT NOT NULL,
                    FOREIGN KEY (lesson_id) REFERENCES lessons(id)
                );";
            command.ExecuteNonQuery();
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
            return (long)command.ExecuteScalar();
        }

        public void ProcessJsonFile(string filePath)
        {
            var json = File.ReadAllText(filePath);
            var groupName = Path.GetFileNameWithoutExtension(filePath);
            var schedule = _scheduleReader.GetSchedule(groupName, json);

            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            // ������
            var groupId = GetOrCreate(connection, "groups", "name", groupName);

            // ������� � ��������
            var teachers = schedule.Days.Select(c => c.Teacher).Distinct().ToList();
            var rooms = schedule.Days.Select(c => c.Cabinet).Where(c => !string.IsNullOrEmpty(c)).Distinct().ToList();

            var teacherIds = new Dictionary<string, long>();
            foreach (var teacher in teachers)
                if (teacher is not null)
                    teacherIds[teacher] = GetOrCreate(connection, "teachers", "name", teacher);

            var roomIds = new Dictionary<string, long>();
            foreach (var room in rooms)
                if (room is not null)
                    roomIds[room] = GetOrCreate(connection, "rooms", "name", room);

            // ����� (�����)
            foreach (var course in schedule.Days)
            {
                using var command = connection.CreateCommand();
                // ������� ������ �����
                command.CommandText = @"
                    INSERT INTO sessions (group_id, start_time, end_time)
                    VALUES (@group_id, @start_time, @end_time);
                    SELECT last_insert_rowid();";

                command.Parameters.AddWithValue("@group_id", groupId);
                command.Parameters.AddWithValue("@start_time", $"{course.StartTime.Hour:D2}:{course.StartTime.Minute:D2}");
                command.Parameters.AddWithValue("@end_time", $"{(course.StartTime + course.Duration).Hour:D2}:{(course.StartTime + course.Duration).Minute:D2}");

                var sessionId = (long)command.ExecuteScalar();

                // ������� ����
                var teacherId = teacherIds[course.Teacher];
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

                var lessonId = (long)command.ExecuteScalar();

                // ��������� ���� (������ dd.MM)
                command.CommandText = @"
                    INSERT INTO schedule_dates (lesson_id, date)
                    VALUES (@lesson_id, @date)";
                command.Parameters.Clear();
                command.Parameters.AddWithValue("@lesson_id", lessonId);
                var dateParam = command.Parameters.Add("@date", SqliteType.Text);

                foreach (var date in course.Dates)
                {
                    dateParam.Value = date.ToString("dd.MM", null);
                    command.ExecuteNonQuery();
                }
            }
        }

        public void ProcessFolder(string folderPath)
        {
            foreach (var file in Directory.GetFiles(folderPath, "*.json"))
            {
                try
                {
                    Console.WriteLine($"processing file: {file}");
                    ProcessJsonFile(file);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"error processing {file}: {ex.Message}");
                }
            }
            Console.WriteLine("database population completed!");
        }

        public List<string> GetGroups()
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

        public List<Course> GetScheduleForGroup(string groupName, LocalDate date)
        {
            var courses = new List<Course>();
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT l.subject, t.name as teacher, l.lesson_type, r.name as room, 
                       s.start_time, s.end_time, l.subgroup, g.name as group_name
                FROM lessons l
                JOIN sessions s ON l.session_id = s.id
                JOIN groups g ON s.group_id = g.id
                JOIN teachers t ON l.teacher_id = t.id
                LEFT JOIN rooms r ON l.room_id = r.id
                JOIN schedule_dates sd ON l.id = sd.lesson_id
                WHERE g.name = @groupName AND sd.date = @date
                ORDER BY s.start_time";


            Console.WriteLine("group = " + groupName);
            Console.WriteLine("date = " + date.ToString("dd.MM", null));

            command.Parameters.AddWithValue("@groupName", groupName);
            // ���������� ������ dd.MM ��� ����
            command.Parameters.AddWithValue("@date", date.ToString("dd.MM", null));

            var s = command.CommandText.ToString();

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var startTimeStr = reader.GetString(4);
                var endTimeStr = reader.GetString(5);
                var startTime = LocalTimePattern.CreateWithInvariantCulture("HH:mm").Parse(startTimeStr).Value;
                var endTime = LocalTimePattern.CreateWithInvariantCulture("HH:mm").Parse(endTimeStr).Value;
                var duration = Period.Between(startTime, endTime);

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
                    Dates = [date]
                });
            }

            return courses;
        }

        public List<Course> GetScheduleForRoom(string roomName, LocalDate date)
        {
            var courses = new List<Course>();
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT l.subject, t.name as teacher, l.lesson_type, r.name as room, 
                       s.start_time, s.end_time, l.subgroup, g.name as group_name
                FROM lessons l
                JOIN sessions s ON l.session_id = s.id
                JOIN groups g ON s.group_id = g.id
                JOIN teachers t ON l.teacher_id = t.id
                JOIN rooms r ON l.room_id = r.id
                JOIN schedule_dates sd ON l.id = sd.lesson_id
                WHERE r.name = @roomName AND sd.date = @date
                ORDER BY s.start_time";

            command.Parameters.AddWithValue("@roomName", roomName);
            command.Parameters.AddWithValue("@date", date.ToString("dd.MM", null));

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var startTimeStr = reader.GetString(4);
                var endTimeStr = reader.GetString(5);
                var startTime = LocalTimePattern.CreateWithInvariantCulture("HH:mm").Parse(startTimeStr).Value;
                var endTime = LocalTimePattern.CreateWithInvariantCulture("HH:mm").Parse(endTimeStr).Value;
                var duration = Period.Between(startTime, endTime);

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
                    Dates = new List<LocalDate> { date }
                });
            }

            return courses;
        }

        public List<string> GetRooms()
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

        public List<string> GetTeachers()
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

        public List<Course> GetScheduleForTeacher(string teacherName, LocalDate date)
        {
            var courses = new List<Course>();
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT l.subject, t.name as teacher, l.lesson_type, r.name as room, 
                       s.start_time, s.end_time, l.subgroup, g.name as group_name
                FROM lessons l
                JOIN sessions s ON l.session_id = s.id
                JOIN groups g ON s.group_id = g.id
                JOIN teachers t ON l.teacher_id = t.id
                LEFT JOIN rooms r ON l.room_id = r.id
                JOIN schedule_dates sd ON l.id = sd.lesson_id
                WHERE t.name = @teacherName AND sd.date = @date
                ORDER BY s.start_time";

            command.Parameters.AddWithValue("@teacherName", teacherName);
            command.Parameters.AddWithValue("@date", date.ToString("dd.MM", null));

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var startTimeStr = reader.GetString(4);
                var endTimeStr = reader.GetString(5);
                var startTime = LocalTimePattern.CreateWithInvariantCulture("HH:mm").Parse(startTimeStr).Value;
                var endTime = LocalTimePattern.CreateWithInvariantCulture("HH:mm").Parse(endTimeStr).Value;
                var duration = Period.Between(startTime, endTime);

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
                    Dates = new List<LocalDate> { date }
                });
            }

            return courses;
        }
    }
} 