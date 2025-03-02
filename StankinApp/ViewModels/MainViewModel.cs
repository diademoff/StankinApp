using CommunityToolkit.Mvvm.ComponentModel;
using StankinApp.Core;
using StankinApp.Core.ScheduleModel;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace StankinApp.ViewModels
{
    public class DateSchedule
    {
        public DateTime Date { get; set; }
        public ObservableCollection<Course> Courses { get; }

        public DateSchedule(DateTime date, Course[] courses)
        {
            Date = date;
            Courses = new ObservableCollection<Course>(courses) ?? throw new ArgumentNullException(nameof(courses));
        }
    }

    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        ObservableCollection<DateSchedule> _schedule;

        public MainViewModel()
        {
            //_courses = new ObservableCollection<Course>(GetCoursesForDate(DateTime.Now));

            Schedule = new ObservableCollection<DateSchedule>(
            [
                new DateSchedule(DateTime.Now, GetCoursesForDate(DateTime.Now).ToArray()),
                new DateSchedule(DateTime.Now.AddDays(1), GetCoursesForDate(DateTime.Now.AddDays(1)).ToArray()),
                new DateSchedule(DateTime.Now.AddDays(2), GetCoursesForDate(DateTime.Now.AddDays(2)).ToArray()),
                new DateSchedule(DateTime.Now.AddDays(3), GetCoursesForDate(DateTime.Now.AddDays(3)).ToArray()),
                new DateSchedule(DateTime.Now.AddDays(4), GetCoursesForDate(DateTime.Now.AddDays(4)).ToArray()),
                new DateSchedule(DateTime.Now.AddDays(5), GetCoursesForDate(DateTime.Now.AddDays(5)).ToArray()),
            ]);

            OnPropertyChanged(nameof(Schedule));
        }

        // TODO: timer
        //private void RedrawTick(object? sender, EventArgs e)
        //{
        //    OnPropertyChanged(nameof(Schedule));
        //}

        List<Course> GetCoursesForDate(DateTime date)
        {
            if (date.DayOfWeek == DayOfWeek.Sunday)
                return [];

            var jsonstr = new StreamReader(FileSystem.OpenAppPackageFileAsync("ADB-23-07.json").Result).ReadToEnd();
            Schedule schedule = ScheduleJsonReader.GetSchedule("АДБ-23-07", jsonstr);

            DayOfWeek todayDayOfWeek = date.DayOfWeek;
            DaySchedule todaySchedule = schedule.Days[(int)todayDayOfWeek - 1];

            List<Course> courses = new List<Course>();
            foreach (var ts in todaySchedule.TimeSlots)
            {
                int count = 0;
                foreach (var c in ts.Courses)
                {
                    if (c.IsInDate(date))
                    {
                        courses.Add(c);
                        count++;
                    }
                }
            }

            return courses;
        }
    }
}