using NodaTime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
