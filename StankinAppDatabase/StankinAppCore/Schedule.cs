namespace StankinAppCore;

public class Schedule
{
    public string GroupName { get; private set; }
    public List<Course> Days { get; private set; }

    public Schedule(string groupName, List<Course> days)
    {
        Days = days ?? throw new ArgumentNullException(nameof(days));
        GroupName = groupName;
    }
}