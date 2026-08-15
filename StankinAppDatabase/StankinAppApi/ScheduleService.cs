using System.Globalization;
using StankinAppCore;
using StankinAppApi.Dto;
using NodaTime;

namespace StankinAppApi;

public class ScheduleService
{
    private readonly IDataReader _db;
    private const double MaxGapMinutes = 30;

    public ScheduleService(IDataReader db) => _db = db;

    public IEnumerable<string> GetGroups()   => _db.GetGroups();
    public IEnumerable<string> GetRooms()    => _db.GetRooms();
    public IEnumerable<string> GetTeachers() => _db.GetTeachers();

    public IEnumerable<CourseDto> GetMergedScheduleForGroup(
        string groupName, string startDate, string endDate)
    {
        var courses = _db.GetScheduleForGroup(groupName, startDate, endDate);
        return MergeAndToDtoList(courses);
    }

    public IEnumerable<CourseDto> GetScheduleBySubject(
        string subjectName, string teacherName, string groupName,
        string startDate, string endDate)
    {
        var courses = _db.GetScheduleBySubject(subjectName, teacherName, groupName, startDate, endDate);
        return MergeAndToDtoList(courses);
    }

    public IEnumerable<CourseDto> GetMergedScheduleForTeacher(
        string teacherName, string startDate, string endDate)
    {
        var courses = _db.GetScheduleForTeacher(teacherName, startDate, endDate);
        return MergeAndToDtoList(courses);
    }

    private static IEnumerable<CourseDto> MergeAndToDtoList(IEnumerable<Course> courses)
    {
        var instances = new List<LessonInstance>();

        foreach (var c in courses)
        {
            foreach (var d in c.Dates)
            {
                var start = d.At(c.StartTime);
                var end   = start + c.Duration;

                instances.Add(new LessonInstance
                {
                    Start            = start,
                    End              = end,
                    Subject          = c.Subject,
                    Teacher          = c.Teacher,
                    Type             = c.Type,
                    Subgroup         = c.Subgroup,
                    Cabinet          = c.Cabinet,
                    GroupName        = c.GroupName,
                    SequencePosition = c.SequencePosition,
                    SequenceLength   = c.SequenceLength
                });
            }
        }

        var mergedDtos = new List<CourseDto>();

        foreach (var dayGroup in instances.GroupBy(i => i.Start.Date))
        {
            foreach (var subGroup in dayGroup.GroupBy(i => i.Subgroup ?? string.Empty))
            {
                var subInstances = subGroup.OrderBy(i => i.Start).ToList();
                if (subInstances.Count == 0) continue;

                var current = subInstances[0];
                for (int i = 1; i < subInstances.Count; i++)
                {
                    var next = subInstances[i];
                    var gap  = (next.Start - current.End).ToDuration();

                    bool canMerge =
                        gap >= Duration.Zero &&
                        gap.TotalMinutes <= MaxGapMinutes &&
                        current.Subject  == next.Subject  &&
                        current.Teacher  == next.Teacher  &&
                        current.Type     == next.Type     &&
                        current.Subgroup == next.Subgroup &&
                        current.Cabinet  == next.Cabinet;

                    if (canMerge)
                        current.End = next.End;
                    else
                    {
                        mergedDtos.Add(ToDto(current));
                        current = next;
                    }
                }
                mergedDtos.Add(ToDto(current));
            }
        }

        return mergedDtos;
    }

    private static CourseDto ToDto(LessonInstance i)
    {
        var dateStr     = i.Start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var startStr    = i.Start.ToString("HH:mm", CultureInfo.InvariantCulture);
        var endStr      = i.End.ToString("HH:mm", CultureInfo.InvariantCulture);
        var durationMin = (int)(i.End - i.Start).ToDuration().TotalMinutes;
        var subgroupKey = string.IsNullOrEmpty(i.Subgroup) ? "all" : i.Subgroup;

        var id = $"{i.GroupName}_{dateStr}_{startStr}_{subgroupKey}"
                 .Replace(" ", "_");

        return new CourseDto(
            Id:               id,
            Date:             dateStr,
            StartTime:        startStr,
            EndTime:          endStr,
            DurationMinutes:  durationMin,
            GroupName:        i.GroupName,
            Subject:          i.Subject,
            Teacher:          i.Teacher,
            Type:             NormalizeType(i.Type),
            Subgroup:         i.Subgroup ?? string.Empty,
            Cabinet:          i.Cabinet,
            SequencePosition: i.SequencePosition,
            SequenceLength:   i.SequenceLength
        );
    }

    private static string NormalizeType(string type) => type switch
    {
        "семинар"               => "Семинар",
        "лекции"                => "Лекция",
        "лабораторные занятия"  => "Лабораторная работа",
        _                       => type
    };
}


internal struct LessonInstance
{
    public LocalDateTime Start { get; set; }
    public LocalDateTime End { get; set; }
    public string Subject { get; set; }
    public string Teacher { get; set; }
    public string Type { get; set; }
    public string Subgroup { get; set; }
    public string Cabinet { get; set; }
    public string GroupName { get; set; }
    public int SequencePosition { get; set; }
    public int SequenceLength { get; set; }
}
