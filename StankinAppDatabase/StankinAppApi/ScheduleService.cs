using StankinAppCore;
using System;
using System.Collections.Generic;
using System.Linq;
using NodaTime;

namespace StankinAppApi;

public interface IScheduleService
{
    IEnumerable<string> GetGroups();
    IEnumerable<string> GetRooms();
    IEnumerable<string> GetTeachers();
    IEnumerable<CourseDto> GetMergedScheduleForGroup(string groupName, string startDate, string endDate);
}

public class ScheduleService : IScheduleService
{
    private readonly IDataReader _db;
    private const double MaxGapMinutes = 30;

    public ScheduleService(IDataReader db)
    {
        _db = db;
    }

    public IEnumerable<string> GetGroups() => _db.GetGroups();
    public IEnumerable<string> GetRooms() => _db.GetRooms();
    public IEnumerable<string> GetTeachers() => _db.GetTeachers();

    public IEnumerable<CourseDto> GetMergedScheduleForGroup(string groupName, string startDate, string endDate)
    {
        var courses = _db.GetScheduleForGroup(groupName, startDate, endDate);
        var instances = new List<LessonInstance>();

        foreach (var c in courses)
        {
            foreach (var d in c.Dates)
            {
                var start = d.At(c.StartTime);
                var end = start + c.Duration;

                instances.Add(new LessonInstance
                {
                    Start = start,
                    End = end,
                    Subject = c.Subject,
                    Teacher = c.Teacher,
                    Type = c.Type,
                    Subgroup = c.Subgroup,
                    Cabinet = c.Cabinet,
                    GroupName = c.GroupName,
                    SequencePosition = c.SequencePosition,
                    SequenceLength = c.SequenceLength
                });
            }
        }

        var groupedByDate = instances.GroupBy(i => i.Start.Date);
        var mergedDtos = new List<CourseDto>();

        foreach (var dayGroup in groupedByDate)
        {
            var subgroupGroups = dayGroup.GroupBy(i => i.Subgroup ?? string.Empty);

            foreach (var subGroup in subgroupGroups)
            {
                var subInstances = subGroup.OrderBy(i => i.Start).ToList();
                if (subInstances.Count == 0) continue;

                var current = subInstances[0];
                for (int i = 1; i < subInstances.Count; i++)
                {
                    var next = subInstances[i];
                    var gap = (next.Start - current.End).ToDuration();
                    if (gap >= Duration.Zero &&
                        gap.TotalMinutes <= MaxGapMinutes &&
                        current.Subject == next.Subject &&
                        current.Teacher == next.Teacher &&
                        current.Type == next.Type &&
                        current.Subgroup == next.Subgroup &&
                        current.Cabinet == next.Cabinet)
                    {
                        current.End = next.End;
                    }
                    else
                    {
                        mergedDtos.Add(CreateDtoFromInstance(current));
                        current = next;
                    }
                }
                mergedDtos.Add(CreateDtoFromInstance(current));
            }
        }

        return mergedDtos;
    }

    private CourseDto CreateDtoFromInstance(LessonInstance i)
    {
        var duration = (i.End - i.Start).ToDuration();
        var durationMinutes = (long)duration.TotalMinutes;
        var dto = new CourseDto
        {
            StartTime = new SimpleTime(i.Start.Hour, i.Start.Minute),
            Duration = new DurationType(durationMinutes),
            Dates = new List<SimpleDate> { new SimpleDate(i.Start.Year, i.Start.Month, i.Start.Day) },
            GroupName = i.GroupName,
            Subject = i.Subject,
            Teacher = i.Teacher,
            Type = NormalizeType(i.Type),
            Subgroup = i.Subgroup,
            Cabinet = i.Cabinet,
            SequencePosition = i.SequencePosition, // Copy from first; adjust if needed based on domain logic
            SequenceLength = i.SequenceLength      // Copy from first; adjust if needed based on domain logic
        };
        return dto;
    }

    private string NormalizeType(string type)
    {
        switch (type)
        {
            case "семинар":
                return "Семинар";
            case "лекции":
                return "Лекция";
            case "лабораторные занятия":
                return "Лабораторная работа";
            default:
                return type;
        }
    }
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