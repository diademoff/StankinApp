using NodaTime;

namespace StankinAppDatabase
{
    public static class HandleErrorMethods
    {
        static ScheduleJsonReader _reader;
        public static int year = DateTime.Now.Year;


        readonly static string[] SubjectNamesWithDots =
        {
            "Технологии индустрии 4.0"
        };

        readonly static Tuple<string, string>[] AnomalyTeachers =
        {
            // Что искать / На что менять
                                    
            new Tuple<string, string>("Амир Абдаллах Д. А.  . .", "Амир Абдаллах Д. А."),
            new Tuple<string, string>("Юсеф Фарах  . .", "Юсеф Фарах"),
            new Tuple<string, string>("Мохаммад Раша  . .", "Мохаммад Раша"),
            new Tuple<string, string>("Агоштинью Адау Какулу . .", "Агоштинью Адау Какулу"),
            new Tuple<string, string>("Агоштинью Адау Какулу  . .", "Агоштинью Адау Какулу"), // да, два пробела
        };

        public static Course[] HandleParseError2025(ErrorParsingInfo err)
        {
            if (_reader is null)
                _reader = new ScheduleJsonReader(year, null);
            // Обработка названий предметов с точкой в названии
            var subjectNameWithDots = SubjectNamesWithDots.Where(x => err.LineToParse.Contains(x));
            var subjectNameWithDotExists = subjectNameWithDots.Any();
            if (subjectNameWithDotExists)
            {
                // Обработать предмет с точкой в названии
                try
                {
                    var subjectNameWithDot = subjectNameWithDots.First();
                    return _reader.ParseLessons(err.LineToParse.Replace(subjectNameWithDot, "subjectName"),
                                err.StartTime, err.Duration, err.GroupName, throwOnFail: true)
                                .Select(x =>
                                {
                                    return x with { Subject = x.Subject == "subjectName" ? subjectNameWithDot : x.Subject };
                                }).ToArray();
                }
                catch
                {

                }
            }

            // Обработать преподавателей
            var anomalyTeachers = AnomalyTeachers.Where(x => err.LineToParse.Contains(x.Item1));
            if (anomalyTeachers.Any())
            {
                try
                {
                    var anomalyTeacher = anomalyTeachers.First();
                    return _reader.ParseLessons(err.LineToParse.Replace(anomalyTeacher.Item1, "Иванов И.И."),
                            err.StartTime, err.Duration, err.GroupName, throwOnFail: true)
                            .Select(x =>
                            {
                                return x with { Teacher = x.Teacher == "Иванов И.И." ? anomalyTeacher.Item2 : x.Teacher };
                            }).ToArray();
                }
                catch
                {
                }
            }

            Console.WriteLine("Ошибка при парсинге строки. Введите данные предметов вручную.");

            Console.WriteLine();
            Console.WriteLine(err.LineToParse);
            Console.WriteLine();


            Console.Write("Введите количество курсов: ");
            int count;
            while (!int.TryParse(Console.ReadLine(), out count) || count < 0)
            {
                Console.Write("Пожалуйста, введите допустимое неотрицательное число: ");
            }

            var courses = new List<Course>(count);

            for (int i = 0; i < count; i++)
            {
                Console.WriteLine($"Введите данные для курса #{i + 1}:");

                LocalTime startTime = err.StartTime;
                Period duration = err.Duration;

                string? groupName = err.GroupName;

                Console.Write("Предмет: ");
                string? subject = Console.ReadLine();

                Console.Write("Преподаватель: ");
                string? teacher = Console.ReadLine();

                Console.Write("Тип: ");
                string? type = Console.ReadLine();

                Console.Write("Подгруппа: ");
                string? subgroup = Console.ReadLine();

                Console.Write("Кабинет: ");
                string? cabinet = Console.ReadLine();

                Console.Write("Даты [00.00-00.00 к.н./ч.н.]: ");
                string? date_str = Console.ReadLine();
                var dates = _reader.ParseSchedule(date_str, year);

                // Создаём объект Course
                var course = new Course
                {
                    StartTime = startTime,
                    Duration = duration,
                    Dates = dates,
                    GroupName = groupName,
                    Subject = subject,
                    Teacher = teacher,
                    Type = type,
                    Subgroup = subgroup,
                    Cabinet = cabinet
                };

                courses.Add(course);
            }

            return courses.ToArray();
        }
    }
}
