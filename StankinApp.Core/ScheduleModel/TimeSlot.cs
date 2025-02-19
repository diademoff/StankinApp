using System;
using System.Collections.Generic;
using System.Text;

namespace StankinApp.Core.ScheduleModel
{
    /// <summary>
    /// Список предментов для заданного времени
    /// </summary>
    public class TimeSlot
    {
        public string Time { get; private set; }

        /// <summary>
        /// Номер пары
        /// </summary>
        public int PairNumber { get; private set; }
        public List<Course> Courses { get; private set; }

        public TimeSlot(string time)
        {
            Time = time ?? throw new ArgumentNullException(nameof(time));

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

            Courses = new List<Course>();
        }
    }
}