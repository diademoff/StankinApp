using System;
using System.Collections.Generic;
using System.Text;

namespace StankinApp.Core.ScheduleModel
{
    public class Time : IComparable<Time>
    {
        public static Tuple<Time, Time>[] PairNumbers =
        {
            new Tuple<Time, Time>(new Time(8, 30), new Time(10, 10)),
            new Tuple<Time, Time>(new Time(10, 20), new Time(12, 00)),
            new Tuple<Time, Time>(new Time(12, 20), new Time(14, 00)),
            new Tuple<Time, Time>(new Time(14, 10), new Time(15, 50)),
            new Tuple<Time, Time>(new Time(16, 00), new Time(17, 40)),
            new Tuple<Time, Time>(new Time(18, 00), new Time(19, 30)),
            new Tuple<Time, Time>(new Time(19, 40), new Time(21, 10)),
            new Tuple<Time, Time>(new Time(21, 20), new Time(22, 50))
        };

        public int Hour { get; set; }
        public int Minute { get; set; }

        public string PrettyString => $"{Hour:D2}:{Minute:D2}";

        public Time(int hour, int minute)
        {
            Hour=hour;
            Minute=minute;
        }

        public static int? GetPairNumber(Time time)
        {
            for (int i = 0; i < PairNumbers.Length; i++)
            {
                if (time >= PairNumbers[i].Item1
                    && time <= PairNumbers[i].Item2)
                    return i + 1;
            }
            return null;
        }

        public static Tuple<Time, Time> GetPairTimeByNumber(int number)
        {
            int index = number - 1;
            return PairNumbers[index];
        }

        public static bool operator <=(Time current, Time other)
        {
            return current.CompareTo(other) <= 0;
        }

        public static bool operator >=(Time current, Time other)
        {
            return current.CompareTo(other) >= 0;
        }

        public int CompareTo(Time other)
        {
            DateTime t = new DateTime(2025, 02, 1, Hour, Minute, 0);
            DateTime o = new DateTime(2025, 02, 1, other.Hour, other.Minute, 0);

            return t.CompareTo(o);
        }
    }
}
