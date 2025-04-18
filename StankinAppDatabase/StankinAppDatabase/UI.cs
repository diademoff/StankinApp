using Terminal.Gui;
using NodaTime;
using NodaTime.Text;

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
        private List<string> _teachers;
        private ListView _teachersListView;
        private bool _isTeacherSelected;

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
                Width = 50,
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

            // Вкладка "Преподаватели"
            try
            {
                _teachers = _builder.GetTeachers();
                var teachersListView = new ListView
                {
                    Width = Dim.Fill(),
                    Height = Dim.Fill()
                };
                teachersListView.SetSource(_teachers);
                var teachersTab = new TabView.Tab("Преподаватели", teachersListView);
                _teachersListView = teachersListView;
                _teachersListView.SelectedItemChanged += OnTeacherSelected;
                _tabView.AddTab(teachersTab, false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка загрузки преподавателей: {ex.Message}");
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
            _isTeacherSelected = false;
            UpdateSchedule();
        }

        private void OnTeacherSelected(ListViewItemEventArgs args)
        {
            _isRoomSelected = false;
            _isTeacherSelected = true;
            UpdateSchedule();
        }

        private void OnGroupSelected(ListViewItemEventArgs args)
        {
            _isRoomSelected = false;
            _isTeacherSelected = false;
            UpdateSchedule();
        }

        private void UpdateSchedule()
        {
            if (_tabView.SelectedTab == null) return;

            List<Course> courses;
            if (_isTeacherSelected)
            {
                if (_teachersListView.SelectedItem == -1) return;
                var selectedTeacher = _teachers[_teachersListView.SelectedItem];
                var localDate = new LocalDate(_dateField.Date.Year, _dateField.Date.Month, _dateField.Date.Day);
                courses = _builder.GetScheduleForTeacher(selectedTeacher, localDate);
            }
            else if (!_isRoomSelected)
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

            if (courses.Count == 0)
            {
                _scheduleListView.SetSource(new List<string>());
                return;
            }    

            var (subjectWidth, typeWidth, groupWidth, subgroupWidth) = CalculateColumnWidths(courses);

            var timePattern = LocalTimePattern.CreateWithInvariantCulture("HH:mm");
            var scheduleItems = courses.Select(c =>
                _isRoomSelected 
                    ? string.Format("{0}-{1}  {2,-" + subjectWidth + "} {3,-" + typeWidth + "}  {4,-" + groupWidth + "} {5}",
                        timePattern.Format(c.StartTime), timePattern.Format(c.StartTime.Plus(c.Duration)),
                        c.Subject, c.Type, c.GroupName, c.Subgroup ?? "")
                    : _isTeacherSelected
                        ? string.Format("{0}-{1}  {2,-" + subjectWidth + "} {3,-" + typeWidth + "}  {4,-" + groupWidth + "} {5,-" + subgroupWidth + "} {6}",
                        timePattern.Format(c.StartTime), timePattern.Format(c.StartTime.Plus(c.Duration)),
                        c.Subject, c.Type, c.GroupName, c.Subgroup ?? "", c.Cabinet ?? "дист.")
                        : string.Format("{0}-{1}  {2,-" + subjectWidth + "} {3,-" + typeWidth + "}  {4,-" + groupWidth + "} {5}",
                        timePattern.Format(c.StartTime), timePattern.Format(c.StartTime.Plus(c.Duration)),
                        c.Subject, c.Type, c.Cabinet ?? "дист.", c.Subgroup ?? "")).ToList();

            _scheduleListView.SetSource(scheduleItems);
        }

        private (int subjectWidth, int typeWidth, int groupWidth, int subgroupWidth) CalculateColumnWidths(List<Course> courses)
        {
            var subjectWidth = Math.Max(30, courses.Max(c => c.Subject?.Length ?? 0));
            var typeWidth = Math.Max(10, courses.Max(c => c.Type?.Length ?? 0));
            var groupWidth = Math.Max(10, courses.Max(c => c.GroupName?.Length ?? 0));
            var subgroupWidth = Math.Max(5, courses.Max(c => c.Subgroup?.Length ?? 0));

            // Add some padding to each column
            subjectWidth += 2;
            typeWidth += 2;
            groupWidth += 2;
            subgroupWidth += 2;

            return (subjectWidth, typeWidth, groupWidth, subgroupWidth);
        }
    }
} 