using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using StankinApp.Core;
using StankinApp.Core.ScheduleModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

namespace StankinApp.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    ObservableCollection<Course> _courses;

    public MainViewModel()
    {
        var jsonStream = AssetLoader.Open(new Uri("avares://StankinApp/Assets/АДБ-23-07.json"));

        StreamReader sr = new StreamReader(jsonStream);
        var jsonstr = sr.ReadToEnd();
        Schedule schedule = ScheduleJsonReader.GetSchedule(jsonstr);
        _courses = new ObservableCollection<Course>(schedule.Days[3].TimeSlots[1].Courses);
    }
}
