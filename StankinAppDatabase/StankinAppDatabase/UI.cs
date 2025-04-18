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

        private List<string> _rooms;
        private TabView _tabView;
        private ListView _roomsListView;
        private bool _isRoomSelected;

        public UI(DatabaseBuilder builder)
        {
            _builder = builder;
        }

        public void Run()
        {
            Application.Init();
            var top = Application.Top;

            // Главное окно
            _mainWindow = new Window("Расписание МГТУ СТАНКИН")
            {
                X = 0,
                Y = 1,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };

            // Меню
            var menu = new MenuBar(new MenuBarItem[]
            {
                new MenuBarItem("_Файл", new MenuItem[]
                {
                    new MenuItem("_Выход", "", () => Application.RequestStop())
                })
            });

            // Левая панель с табами
            var leftPanel = new FrameView("")
            {
                X = 0,
                Y = 0,
                Width = 30,
                Height = Dim.Fill(1)
            };

            // Инициализация TabView
            _tabView = new TabView()
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };

            // Вкладка "Группы"
            try
            {
                _groups = _builder.GetGroups();
                var groupsListView = new ListView
                {
                    Width = Dim.Fill(),
                    Height = Dim.Fill()
                };
                groupsListView.SetSource(_groups);
                var groupsTab = new TabView.Tab("Группы", groupsListView);
                _groupsListView = groupsListView;
                _groupsListView.SelectedItemChanged += OnGroupSelected;
                _tabView.AddTab(groupsTab, false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка загрузки групп: {ex.Message}");
            }

            // Вкладка "Кабинеты"
            try
            {
                _rooms = _builder.GetRooms();
                var roomsListView = new ListView
                {
                    Width = Dim.Fill(),
                    Height = Dim.Fill()
                };
                roomsListView.SetSource(_rooms);
                var roomsTab = new TabView.Tab("Кабинеты", roomsListView);
                _roomsListView = roomsListView;
                _roomsListView.SelectedItemChanged += OnRoomSelected;
                _tabView.AddTab(roomsTab, false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка загрузки кабинетов: {ex.Message}");
            }

            leftPanel.Add(_tabView);

            // Правая панель
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
            _dateField.DateChanged += (args) => UpdateSchedule();

            _scheduleListView = new ListView()
            {
                X = 0,
                Y = 2,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };

            scheduleFrame.Add(_dateField, _scheduleListView);
            _mainWindow.Add(leftPanel, scheduleFrame);
            top.Add(menu, _mainWindow);

            Application.Run();
        }

        private void OnDateChanged(DateTimeEventArgs<DateTime> args)
        {
            UpdateSchedule();
        }

        private void OnRoomSelected(ListViewItemEventArgs args)
        {
            _isRoomSelected = true;
            UpdateSchedule();
        }

        private void OnGroupSelected(ListViewItemEventArgs args)
        {
            _isRoomSelected = false;
            UpdateSchedule();
        }

        private void UpdateSchedule()
        {
            if (_tabView.SelectedTab == null) return;

            List<Course> courses;
            if (!_isRoomSelected)
            {
                if (_groupsListView.SelectedItem == -1) return;
                var selectedGroup = _groups[_groupsListView.SelectedItem];
                var localDate = new LocalDate(_dateField.Date.Year, _dateField.Date.Month, _dateField.Date.Day);
                courses = _builder.GetScheduleForGroup(selectedGroup, localDate);
            }
            else
            {
                if (_roomsListView.SelectedItem == -1) return;
                var selectedRoom = _rooms[_roomsListView.SelectedItem];
                var localDate = new LocalDate(_dateField.Date.Year, _dateField.Date.Month, _dateField.Date.Day);
                courses = _builder.GetScheduleForRoom(selectedRoom, localDate);
            }

            var scheduleItems = courses.Select(c =>
                _isRoomSelected 
                    ? $"{c.StartTime:HH:mm}-{c.StartTime.Plus(c.Duration):HH:mm} {c.Subject} ({c.Type}) {c.GroupName} {c.Subgroup}"
                    : $"{c.StartTime:HH:mm}-{c.StartTime.Plus(c.Duration):HH:mm} {c.Subject} ({c.Type}) {c.Cabinet ?? "дист."}").ToList();

            _scheduleListView.SetSource(scheduleItems);
        }
    }
} 