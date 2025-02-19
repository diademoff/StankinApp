using System;
using System.Collections.Generic;

namespace StankinApp.Core.ScheduleModel
{
    /// <summary>
    /// Расписание для конкретной группы
    /// </summary>
    public class Schedule
    {
        public List<DaySchedule> Days { get; private set; }

        public Schedule(List<DaySchedule> days)
        {
            Days = days ?? throw new ArgumentNullException(nameof(days));
        }
    }
}
