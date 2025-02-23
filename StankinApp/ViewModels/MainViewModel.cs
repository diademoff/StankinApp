using Avalonia.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using StankinApp.Core;
using StankinApp.Core.ScheduleModel;
using StankinApp.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

namespace StankinApp.ViewModels;

public partial class MainViewModel : ObservableObject
{
    //[ObservableProperty]
    //ObservableCollection<Course> _courses;

    [ObservableProperty]
    ObservableCollection<DateSchedule> _schedule;

    public MainViewModel()
    {
        //_courses = new ObservableCollection<Course>(GetCoursesForDate(DateTime.Now));

        _schedule = new ObservableCollection<DateSchedule>(
        [
            new DateSchedule(DateTime.Now, GetCoursesForDate(DateTime.Now).ToArray()),
            new DateSchedule(DateTime.Now.AddDays(1), GetCoursesForDate(DateTime.Now.AddDays(1)).ToArray()),
            new DateSchedule(DateTime.Now.AddDays(2), GetCoursesForDate(DateTime.Now.AddDays(2)).ToArray()),
            new DateSchedule(DateTime.Now.AddDays(3), GetCoursesForDate(DateTime.Now.AddDays(3)).ToArray()),
            new DateSchedule(DateTime.Now.AddDays(4), GetCoursesForDate(DateTime.Now.AddDays(4)).ToArray()),
            new DateSchedule(DateTime.Now.AddDays(5), GetCoursesForDate(DateTime.Now.AddDays(5)).ToArray()),
        ]);

        var timer = new DispatcherTimer();
        timer.Interval = TimeSpan.FromSeconds(10);
        timer.Tick += RedrawTick;
        timer.Start();
    }

    private void RedrawTick(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(Schedule));
    }

    List<Course> GetCoursesForDate(DateTime date)
    {
        if (date.DayOfWeek == DayOfWeek.Sunday)
            return [];

        var jsonStream = AssetLoader.Open(new Uri("avares://StankinApp/Assets/АДБ-23-07.json"));

        StreamReader sr = new StreamReader(jsonStream);
        var jsonstr = sr.ReadToEnd();
        Schedule schedule = ScheduleJsonReader.GetSchedule(jsonstr);

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
