using System;

namespace StankinApp.Core.ScheduleModel
{
    public struct PairTime
    {
        public Time TimeBegin { get; private set; }
        public Time TimeEnd { get; private set; }
        public string TimeBeginPretty => $"{TimeBegin.Hour:D2}:{TimeBegin.Minute:D2}";
        public string TimeEndPretty => $"{TimeEnd.Hour:D2}:{TimeEnd.Minute:D2}";
        public DayOfWeek DayOfWeek { get; private set; }

        /// <summary>
        /// Номер пары
        /// </summary>
        public int PairNumber { get; private set; }

        public PairTime(string time, DayOfWeek dayOfWeek)
        {
            if (time is null)
                throw new ArgumentNullException(nameof(time));

            string begin = time.Split('-')[0];
            TimeBegin = new Time(int.Parse(begin.Split(':')[0]), int.Parse(begin.Split(':')[1]));

            string end = time.Split('-')[1];
            TimeEnd = new Time(int.Parse(end.Split(':')[0]), int.Parse(end.Split(':')[1]));

            DayOfWeek = dayOfWeek;

            if (Time.GetPairNumber(TimeBegin) != Time.GetPairNumber(TimeEnd))
                throw new Exception("Unexpected time logic");

            PairNumber = Time.GetPairNumber(TimeBegin) ?? throw new Exception("unexpected time");
        }

        public bool Contains(Time t)
        {
            return t >= TimeBegin && t <= TimeEnd;
        }
    }
}
