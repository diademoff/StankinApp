namespace StankinAppCore;

public interface IDataReader
{
    public IEnumerable<string> GetGroups();
    public IEnumerable<string> GetRooms();
    public IEnumerable<string> GetTeachers();
    public IEnumerable<Course> GetScheduleForGroup(string groupName, string startDate, string endDate);
    public IEnumerable<Course> GetScheduleForRoom(string roomName, string startDate, string endDate);
    public IEnumerable<Course> GetScheduleForTeacher(string teacherName, string startDate, string endDate);
}