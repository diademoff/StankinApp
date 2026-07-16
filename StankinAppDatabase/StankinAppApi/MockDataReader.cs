using System.Globalization;
using NodaTime;
using StankinAppCore;

namespace StankinAppApi;

internal sealed class MockDataReader : IDataReader
{
    private static readonly string[] Groups = ["ИДБ-22-01", "АДБ-23-04"];
    private static readonly string[] Rooms = ["301", "302"];
    private static readonly string[] Teachers = ["Иванов И.И.", "Петров П.П."];

    public IEnumerable<string> GetGroups() => Groups;
    public IEnumerable<string> GetRooms() => Rooms;
    public IEnumerable<string> GetTeachers() => Teachers;

    public IEnumerable<Course> GetScheduleForGroup(string groupName, string startDate, string endDate)
    {
        if (groupName != Groups[0]) return [];

        var start = DateOnly.ParseExact(startDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var end = DateOnly.ParseExact(endDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);

        var result = new List<Course>();
        for (var d = start; d <= end; d = d.AddDays(1))
        {
            if (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                continue; // ponytail: no weekend classes in mock

            var date = new LocalDate(d.Year, d.Month, d.Day);
            result.AddRange([
                new Course
                {
                    Subject = "Математика",
                    Teacher = Teachers[0],
                    Type = "Лекция",
                    Cabinet = Rooms[0],
                    StartTime = new LocalTime(8, 30),
                    Duration = Period.FromMinutes(90),
                    GroupName = groupName,
                    Dates = [date],
                    SequencePosition = 1,
                    SequenceLength = 16,
                },
                new Course
                {
                    Subject = "Программирование",
                    Teacher = Teachers[1],
                    Type = "Семинар",
                    Cabinet = Rooms[1],
                    StartTime = new LocalTime(10, 15),
                    Duration = Period.FromMinutes(90),
                    Subgroup = "1",
                    GroupName = groupName,
                    Dates = [date],
                    SequencePosition = 1,
                    SequenceLength = 16,
                },
            ]);
        }

        return result;
    }

    public IEnumerable<Course> GetScheduleForRoom(string roomName, string startDate, string endDate) => [];
    public IEnumerable<Course> GetScheduleForTeacher(string teacherName, string startDate, string endDate) => [];
}
