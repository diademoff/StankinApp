using NodaTime;
using StankinAppCore;

namespace StankinAppDatabase
{
    public struct ErrorParsingInfo
    {
        public string LineToParse;
        public string GroupName;
        public LocalTime StartTime;
        public Period Duration;
        public List<Course> FailedToParseCourses;
    }
}
