using NodaTime;

namespace StankinAppCore;

public struct Course
{
    public LocalTime StartTime { get; set; }
    public Period Duration { get; set; }
    public List<LocalDate>? Dates { get; set; }
    public string? GroupName { get; set; }
    public string? Subject { get; set; }
    public string? Teacher { get; set; }
    public string? Type { get; set; }
    public string? Subgroup { get; set; }
    public string? Cabinet { get; set; }

    public override readonly string ToString()
    {
        if (Duration is null || Dates is null)
            throw new Exception("Course Duration or Dates is null");
        var endTime = StartTime + Duration;
        var dates = string.Join(", ", Dates.Select(d => d.ToString("dd.MM", null)));
        var subgroupInfo = !string.IsNullOrEmpty(Subgroup) ? $" ({Subgroup})" : "";
        var cabinetInfo = !string.IsNullOrEmpty(Cabinet) ? $" в {Cabinet}" : "";

        return $"{StartTime:HH:mm}-{endTime:HH:mm} | {Subject}{subgroupInfo} | {Type} | {Teacher}{cabinetInfo} | {dates}";
    }
}