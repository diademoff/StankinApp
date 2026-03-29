using StankinAppCore;
using StankinAppApi.Dto;
using NodaTime;

namespace StankinAppApi;

public interface IScheduleService
{
    IEnumerable<string> GetGroups();
    IEnumerable<string> GetRooms();
    IEnumerable<string> GetTeachers();

    /// <summary>
    /// Возвращает плоский список занятий за период.
    /// startDate / endDate — строки "yyyy-MM-dd".
    /// </summary>
    IEnumerable<CourseDto> GetMergedScheduleForGroup(string groupName, string startDate, string endDate);
}

public class ScheduleService : IScheduleService
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
        var courses   = _db.GetScheduleForGroup(groupName, startDate, endDate);
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
        var dateStr     = $"{i.Start.Year:D4}-{i.Start.Month:D2}-{i.Start.Day:D2}";
        var startStr    = $"{i.Start.Hour:D2}:{i.Start.Minute:D2}";
        var endStr      = $"{i.End.Hour:D2}:{i.End.Minute:D2}";
        var durationMin = (int)(i.End - i.Start).ToDuration().TotalMinutes;
        var subgroupKey = string.IsNullOrEmpty(i.Subgroup) ? "all" : i.Subgroup;

        // Уникальный id для Alpine x-for :key
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