using StankinAppCore;

namespace StankinAppApi.Dto;

public struct CourseDto
{
    public SimpleTime StartTime { get; set; }
    public DurationType Duration { get; set; }
    public List<SimpleDate> Dates { get; set; }
    public string GroupName { get; set; }
    public string Subject { get; set; }
    public string Teacher { get; set; }
    public string Type { get; set; }
    public string Subgroup { get; set; }
    public string Cabinet { get; set; }
    public int SequencePosition { get; set; }
    public int SequenceLength { get; set; }

    public CourseDto(Course c)
    {
        this.StartTime = new SimpleTime(c.StartTime.Hour, c.StartTime.Minute);
        this.Duration = new DurationType(c.Duration.Hours * 60 + c.Duration.Minutes);
        this.Dates = c.Dates.Select(x => new SimpleDate(x.Year, x.Month, x.Day)).ToList();
        this.GroupName = c.GroupName;
        this.Subject = c.Subject;
        this.Teacher = c.Teacher;
        switch (c.Type)
        {
            case "семинар":
                this.Type = "Семинар";
                break;
            case "лекции":
                this.Type = "Лекция";
                break;
            case "лабораторные занятия":
                this.Type = "Лабораторная работа";
                break;
            default:
                this.Type = c.Type;
                break;
        }
        this.Subgroup = c.Subgroup;
        this.Cabinet = c.Cabinet;
        this.SequencePosition = c.SequencePosition;
        this.SequenceLength = c.SequenceLength;
    }
}

public class SimpleTime
{
    public int Hour { get; set; }
    public int Minute { get; set; }

    public SimpleTime(int hour, int minute)
    {
        Hour = hour;
        Minute = minute;
    }
}

public class SimpleDate
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int Day { get; set; }

    public SimpleDate(int year, int month, int day)
    {
        Year = year;
        Month = month;
        Day = day;
    }
}

public class DurationType
{
    public long Minutes { get; set; }

    public DurationType(long minutes)
    {
        Minutes = minutes;
    }
}