using NodaTime;

namespace StankinAppDatabase
{
    public struct ErrorParsingInfo
    {
        public string LineToParse;
        public string GroupName;
        public LocalTime StartTime;
        public Period Duration;
    }
}
