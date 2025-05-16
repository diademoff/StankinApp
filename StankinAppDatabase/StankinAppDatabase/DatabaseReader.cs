using Microsoft.Data.Sqlite;
using NodaTime;
using NodaTime.Text;

namespace StankinAppDatabase
{
    public class DatabaseReader
    {
        private readonly string _dbPath;

        public DatabaseReader(string dbPath)
        {
            _dbPath = dbPath;
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
