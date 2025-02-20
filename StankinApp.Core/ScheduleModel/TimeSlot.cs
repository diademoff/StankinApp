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
        public PairTime SlotTime { get; private set; }
        public List<Course> Courses { get; private set; }

        public TimeSlot(string time, DayOfWeek dayOfWeek)
        {

            SlotTime = new PairTime(time, dayOfWeek);
            Courses = new List<Course>();
        }
    }
}