using Terminal.Gui;
using NodaTime;

namespace StankinAppDatabase
{
    public class UI
    {
        private readonly DatabaseBuilder _builder;
        private List<string> _groups;
        private ListView _groupsListView;
        private ListView _scheduleListView;
        private DateField _dateField;
        private Window _mainWindow;

        public UI(DatabaseBuilder builder)
        {
            _builder = builder;
        }

        public void Run()
        {
            Application.Init();
            var top = Application.Top;

            // Create the main window
            _mainWindow = new Window("Расписание МГТУ СТАНКИН")
            {
                X = 0,
                Y = 1,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };

            // Create menu bar
            var menu = new MenuBar(new MenuBarItem[]
            {
                new MenuBarItem("_Файл", new MenuItem[]
                {
                    new MenuItem("_Выход", "", () => Application.RequestStop())
                })
            });

            // Create left panel with groups
            var groupsFrame = new FrameView("Группы")
            {
                X = 0,
                Y = 0,
                Width = 30,
                Height = Dim.Fill()
            };

            _groups = _builder.GetGroups();
            _groupsListView = new ListView(_groups)
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };
            _groupsListView.SelectedItemChanged += OnGroupSelected;
            groupsFrame.Add(_groupsListView);

            // Create right panel with schedule
            var scheduleFrame = new FrameView("Расписание")
            {
                X = 31,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };

            _dateField = new DateField(DateTime.Now)
            {
                X = 0,
                Y = 0,
                Width = 20
            };
            _dateField.DateChanged += (DateTimeEventArgs<DateTime> args) => OnDateChanged(args);

            _scheduleListView = new ListView()
            {
                X = 0,
                Y = 2,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };

            scheduleFrame.Add(_dateField, _scheduleListView);

            _mainWindow.Add(groupsFrame, scheduleFrame);
            top.Add(menu, _mainWindow);

            Application.Run();
        }

        private void OnGroupSelected(ListViewItemEventArgs args)
        {
            UpdateSchedule();
        }

        private void OnDateChanged(DateTimeEventArgs<DateTime> args)
        {
            UpdateSchedule();
        }

        private void UpdateSchedule()
        {
            if (_groupsListView.SelectedItem == -1) return;

            var selectedGroup = _groups[_groupsListView.SelectedItem];
            var date = _dateField.Date;
            var localDate = new LocalDate(date.Year, date.Month, date.Day);
            
            var courses = _builder.GetScheduleForGroup(selectedGroup, localDate);
            var scheduleItems = courses.Select(c => $"{c.StartTime:HH:mm}-{c.StartTime.Plus(c.Duration):HH:mm} {c.Subject} ({c.Type}) {c.Cabinet}").ToList();
            
            _scheduleListView.SetSource(scheduleItems);
        }
    }
} 