using System;

namespace StankinApp.Core.ScheduleModel
{
    public struct PairTime
    {
        public DateTime TimeBegin { get; private set; }
        public DateTime TimeEnd { get; private set; }
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
            TimeBegin = new DateTime(2000, 1, 1, int.Parse(begin.Split(':')[0]), int.Parse(begin.Split(':')[1]), 0);
            string end = time.Split('-')[1];
            TimeEnd = new DateTime(2000, 1, 1, int.Parse(end.Split(':')[0]), int.Parse(end.Split(':')[1]), 0);

            DayOfWeek = dayOfWeek;

            switch (time)
            {
                case "8:30-10:10":
                    PairNumber = 1;
                    break;
                case "10:20-12:00":
                    PairNumber = 2;
                    break;
                case "12:20-14:00":
                    PairNumber = 3;
                    break;
                case "14:10-15:50":
                    PairNumber = 4;
                    break;
                case "16:00-17:40":
                    PairNumber = 5;
                    break;
                case "18:00-19:30":
                    PairNumber = 6;
                    break;
                case "19:40-21:10":
                    PairNumber = 7;
                    break;
                case "21:20-22:50":
                    PairNumber = 8;
                    break;
                default:
                    throw new ArgumentException($"unknown {nameof(time)}");
            }
        }
    }
}
