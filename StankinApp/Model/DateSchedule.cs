using StankinApp.Core.ScheduleModel;
using System;
using System.Collections.ObjectModel;

namespace StankinApp.Model
{
    public class DateSchedule
    {
        public DateTime Date { get; set; }
        public ObservableCollection<Course> Courses { get; set; }

        public DateSchedule(DateTime date, Course[] courses)
        {
            Date = date;
            Courses = new ObservableCollection<Course>(courses) ?? throw new ArgumentNullException(nameof(courses));
        }
    }
}
