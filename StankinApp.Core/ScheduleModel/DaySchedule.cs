using System;
using System.Collections.Generic;

namespace StankinApp.Core.ScheduleModel
{
    /// <summary>
    /// Расписание на заданный день недели Пн-Сб
    /// </summary>
    public class DaySchedule
    {
        public DayOfWeek Day { get; private set; }
        public List<TimeSlot> TimeSlots { get; private set; }

        public DaySchedule(string day)
        {
            if (day is null)
                throw new ArgumentNullException($"{nameof(day)} is null");

            switch (day)
            {
                case "Понедельник":
                    Day = DayOfWeek.Monday;
                    break;
                case "Вторник":
                    Day = DayOfWeek.Tuesday;
                    break;
                case "Среда":
                    Day = DayOfWeek.Wednesday;
                    break;
                case "Четверг":
                    Day = DayOfWeek.Thursday;
                    break;
                case "Пятница":
                    Day = DayOfWeek.Friday;
                    break;
                case "Суббота":
                    Day = DayOfWeek.Saturday;
                    break;
                default:
                    throw new ArgumentException("unrecognized day of week");
            }

            TimeSlots = new List<TimeSlot>();
        }
    }
}
