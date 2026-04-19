using Newtonsoft.Json;
using StankinAppCore;
using System.Globalization;

namespace StankinAppDatabase
{
    public class ParserFallback
    {
        private readonly int _year;
        private readonly string _filePath;
        private ScheduleJsonReader? _reader;
        private const string TemporarySubject = "TEMPORARY_SUBJECT";
        private const string TemporaryTeacher = "Иванов А.Б.";

        // Сколько раз подряд пользователь может нажать Enter без ввода,
        // прежде чем строка будет пропущена автоматически
        private const int MaxEmptyAttempts = 3;

        public List<string> SubjectNamesWithDots { get; private set; }
        public List<TeacherCorrection> AnomalyTeachers { get; private set; }

        public ParserFallback(int year, string path)
        {
            _year = year;
            _filePath = path;
            LoadFallbacks();
        }

        private void LoadFallbacks()
        {
            if (!File.Exists(_filePath))
            {
                SubjectNamesWithDots = new List<string>();
                AnomalyTeachers = new List<TeacherCorrection>();
                return;
            }

            var json = File.ReadAllText(_filePath);
            var data = JsonConvert.DeserializeObject<FallbackData>(json);

            SubjectNamesWithDots = data?.SubjectNamesWithDots ?? new List<string>();
            AnomalyTeachers = data?.AnomalyTeachers ?? new List<TeacherCorrection>();
        }

        private void SaveFallbacks()
        {
            var data = new FallbackData
            {
                SubjectNamesWithDots = SubjectNamesWithDots,
                AnomalyTeachers = AnomalyTeachers
            };

            var json = JsonConvert.SerializeObject(data, Formatting.Indented);
            File.WriteAllText(_filePath, json);
        }

        public Course[] ParseFallbackFunc(ErrorParsingInfo err)
        {
            _reader ??= new ScheduleJsonReader(_year, _ => Array.Empty<Course>());

            if (TryHandleDottedSubjects(err, out var courses)) return courses;
            if (TryHandleAnomalyTeachers(err, out courses)) return courses;
            if (TryHandleBothDottedSubjectsAndAnomalyTeachers(err, out courses)) return courses;

            return HandleParseError(err);
        }

        private bool TryHandleDottedSubjects(ErrorParsingInfo err, out Course[] courses)
        {
            var foundSubject = SubjectNamesWithDots.FirstOrDefault(err.LineToParse.Contains);
            if (foundSubject == null)
            {
                courses = Array.Empty<Course>();
                return false;
            }

            try
            {
                courses = _reader!.ParseLessons(
                    err.LineToParse.Replace(foundSubject, TemporarySubject),
                    err.StartTime,
                    err.Duration,
                    err.GroupName,
                    throwOnFail: true)
                    .Select(x => x with { Subject = x.Subject == TemporarySubject ? foundSubject : x.Subject })
                    .ToArray();
                return true;
            }
            catch
            {
                courses = Array.Empty<Course>();
                return false;
            }
        }

        private bool TryHandleAnomalyTeachers(ErrorParsingInfo err, out Course[] courses)
        {
            var foundTeacher = AnomalyTeachers.FirstOrDefault(x => err.LineToParse.Contains(x.IncorrectName));
            if (foundTeacher == null)
            {
                courses = Array.Empty<Course>();
                return false;
            }

            try
            {
                courses = _reader!.ParseLessons(
                    err.LineToParse.Replace(foundTeacher.IncorrectName, TemporaryTeacher),
                    err.StartTime,
                    err.Duration,
                    err.GroupName,
                    throwOnFail: true)
                    .Select(x => x with { Teacher = x.Teacher == TemporaryTeacher ? foundTeacher.CorrectName : x.Teacher })
                    .ToArray();
                return true;
            }
            catch
            {
                courses = Array.Empty<Course>();
                return false;
            }
        }

        private bool TryHandleBothDottedSubjectsAndAnomalyTeachers(ErrorParsingInfo err, out Course[] courses)
        {
            courses = Array.Empty<Course>();

            var foundSubject = SubjectNamesWithDots.FirstOrDefault(err.LineToParse.Contains);
            var foundTeacher = AnomalyTeachers.FirstOrDefault(x => err.LineToParse.Contains(x.IncorrectName));

            if (foundSubject == null || foundTeacher == null)
                return false;

            try
            {
                var tempLine = err.LineToParse
                    .Replace(foundSubject, TemporarySubject)
                    .Replace(foundTeacher.IncorrectName, TemporaryTeacher);

                courses = _reader!.ParseLessons(
                    tempLine,
                    err.StartTime,
                    err.Duration,
                    err.GroupName,
                    throwOnFail: true)
                    .Select(x => x with
                    {
                        Subject = x.Subject == TemporarySubject ? foundSubject : x.Subject,
                        Teacher = x.Teacher == TemporaryTeacher ? foundTeacher.CorrectName : x.Teacher
                    })
                    .ToArray();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private Course[] HandleParseError(ErrorParsingInfo err)
        {
            DisplayErrorBanner(err);
            DisplayParsedVariants(err);
            DisplayHints(err);

            var correction = AskUserForCorrection(err);

            if (correction == null)
            {
                PrintWarning("Строка пропущена и не будет добавлена в базу данных.");
                return Array.Empty<Course>();
            }

            ApplyCorrection(correction);
            SaveFallbacks();
            PrintSuccess($"Исправление сохранено в {_filePath}. Повторная попытка парсинга...");

            return ParseFallbackFunc(err);
        }

        private static void DisplayErrorBanner(ErrorParsingInfo err)
        {
            var separator = new string('─', 60);
            Console.WriteLine();
            PrintColor($"┌{separator}┐", ConsoleColor.Yellow);
            PrintColor($"│  ⚠  Не удалось распарсить строку расписания{new string(' ', 16)}│", ConsoleColor.Yellow);
            PrintColor($"│  Группа: {err.GroupName,-50}│", ConsoleColor.Yellow);
            PrintColor($"└{separator}┘", ConsoleColor.Yellow);
            Console.WriteLine();
            Console.WriteLine("  Исходная строка:");
            PrintColor($"  > {err.LineToParse}", ConsoleColor.Cyan);
            Console.WriteLine();
        }

        private static void DisplayParsedVariants(ErrorParsingInfo err)
        {
            if (err.FailedToParseCourses.Count == 0)
            {
                PrintColor("  Парсер не смог извлечь ни одного занятия из этой строки.", ConsoleColor.DarkYellow);
                Console.WriteLine();
                return;
            }

            Console.WriteLine("  Парсер попытался извлечь следующие занятия (с ошибками):");
            Console.WriteLine();
            for (int i = 0; i < err.FailedToParseCourses.Count; i++)
            {
                var course = err.FailedToParseCourses[i];
                PrintColor($"  [{i + 1}]", ConsoleColor.White);
                Console.WriteLine($"      Предмет:      {course.Subject}");
                Console.WriteLine($"      Преподаватель:{course.Teacher}");
                Console.WriteLine($"      Тип:          {course.Type}");
                Console.WriteLine();
            }
        }

        private static void DisplayHints(ErrorParsingInfo err)
        {
            Console.WriteLine("  Возможные причины ошибки:");

            // Подсказка: точка в названии предмета
            bool likelyDottedSubject = err.FailedToParseCourses.Any(c =>
                !string.IsNullOrWhiteSpace(c.Subject) && c.Subject.Length <= 3);

            // Подсказка: преподаватель не в формате Фамилия И.О.
            bool likelyAnomalyTeacher = err.FailedToParseCourses.Any(c =>
                !string.IsNullOrWhiteSpace(c.Teacher) &&
                !System.Text.RegularExpressions.Regex.IsMatch(c.Teacher, @"^[А-ЯЁ][а-яё]+\s+[А-ЯЁ]\.[А-ЯЁ]\.$"));

            if (likelyDottedSubject)
                PrintColor("  • Название предмета содержит точку (парсер принял её за разделитель).", ConsoleColor.DarkYellow);

            if (likelyAnomalyTeacher)
                PrintColor("  • ФИО преподавателя не в стандартном формате «Иванов И.И.».", ConsoleColor.DarkYellow);

            if (!likelyDottedSubject && !likelyAnomalyTeacher)
                PrintColor("  • Строка не соответствует ожидаемому формату.", ConsoleColor.DarkYellow);

            Console.WriteLine();
            PrintColor("  Справка: см. README.md → раздел «Что делать при ошибке парсинга»", ConsoleColor.DarkGray);
            Console.WriteLine();
        }

        private Correction? AskUserForCorrection(ErrorParsingInfo err)
        {
            Console.WriteLine("  Выберите тип ошибки или пропустите строку:");
            Console.WriteLine();
            PrintColor("  [1]", ConsoleColor.Green);
            Console.WriteLine(" Предмет с точкой в названии");
            PrintColor("  [2]", ConsoleColor.Green);
            Console.WriteLine(" Нестандартное ФИО преподавателя");
            Console.WriteLine();

            while (true)
            {
                Console.Write("  Ваш выбор: ");
                var choice = (Console.ReadLine() ?? "").Trim().ToLowerInvariant();

                switch (choice)
                {
                    case "1":
                        return AskForDottedSubjectCorrection(err);
                    case "2":
                        return AskForTeacherCorrection(err);
                    case "":
                        return null;
                    default:
                        PrintColor("  Введите 1 или 2", ConsoleColor.Red);
                        break;
                }
            }
        }

        private Correction? AskForDottedSubjectCorrection(ErrorParsingInfo err)
        {
            Console.WriteLine();
            Console.WriteLine("  Введите полное правильное название предмета (с точкой).");
            Console.WriteLine("  Пример: «Технологии индустрии 4.0»");
            Console.WriteLine();

            string? value = PromptNonEmpty("  Название предмета: ");
            if (value == null) return null;

            // Показываем строку с подсветкой введённого значения для проверки
            var highlighted = err.LineToParse.Replace(value,
                $"\x1b[93m{value}\x1b[0m");  // жёлтый ANSI
            Console.WriteLine();
            Console.WriteLine($"  Строка с подсветкой предмета: {highlighted}");
            Console.WriteLine();

            return new Correction(CorrectionType.DottedSubject, value, value);
        }

        private Correction? AskForTeacherCorrection(ErrorParsingInfo err)
        {
            Console.WriteLine();
            Console.WriteLine("  Введите ФИО преподавателя так, как оно стоит в строке (неверное).");

            var knownTeachers = err.FailedToParseCourses
                .Select(c => c.Teacher)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct()
                .ToList();

            if (knownTeachers.Count > 0)
            {
                Console.WriteLine("  Парсер извлёк следующих преподавателей (скопируйте нужное):");
                for (int i = 0; i < knownTeachers.Count; i++)
                    PrintColor($"    [{i + 1}] {knownTeachers[i]}", ConsoleColor.Cyan);
                Console.WriteLine();
            }

            string? incorrectName = PromptNonEmpty("  Неверное ФИО: ");
            if (incorrectName == null) return null;

            Console.WriteLine();
            Console.WriteLine("  Теперь введите правильное отображаемое имя.");
            Console.WriteLine("  Если менять ничего не нужно — нажмите Enter, будет использовано введённое имя.");
            Console.Write("  Правильное ФИО: ");
            var correctName = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(correctName))
                correctName = incorrectName;

            Console.WriteLine();
            Console.WriteLine($"  Будет добавлено: «{incorrectName}» → «{correctName}»");

            if (!Confirm("  Подтверждаете?"))
                return null;

            return new Correction(CorrectionType.AnomalyTeacher, incorrectName, correctName);
        }


        private void ApplyCorrection(Correction correction)
        {
            switch (correction.Type)
            {
                case CorrectionType.DottedSubject:
                    if (!SubjectNamesWithDots.Contains(correction.NewValue))
                    {
                        SubjectNamesWithDots.Add(correction.NewValue);
                        PrintSuccess($"Добавлен предмет с точкой: «{correction.NewValue}»");
                    }
                    else
                    {
                        PrintColor("  Этот предмет уже есть в списке.", ConsoleColor.DarkYellow);
                    }
                    break;

                case CorrectionType.AnomalyTeacher:
                    var existing = AnomalyTeachers.FirstOrDefault(t => t.IncorrectName == correction.OldValue);
                    if (existing == null)
                    {
                        AnomalyTeachers.Add(new TeacherCorrection(correction.OldValue, correction.NewValue));
                        PrintSuccess($"Добавлен преподаватель: «{correction.OldValue}» → «{correction.NewValue}»");
                    }
                    else
                    {
                        PrintColor($"  Преподаватель «{correction.OldValue}» уже есть в списке.", ConsoleColor.DarkYellow);
                    }
                    break;
            }
        }

        /// <summary>
        /// Запрашивает строку, повторяя приглашение при пустом вводе.
        /// Возвращает null, если пользователь несколько раз подряд ввёл пустую строку.
        /// </summary>
        private static string? PromptNonEmpty(string prompt)
        {
            int emptyCount = 0;
            while (emptyCount < MaxEmptyAttempts)
            {
                Console.Write(prompt);
                var input = Console.ReadLine()?.Trim();
                if (!string.IsNullOrWhiteSpace(input))
                    return input;

                emptyCount++;
                int remaining = MaxEmptyAttempts - emptyCount;
                if (remaining > 0)
                    PrintColor($"  Пустой ввод. Попробуйте ещё раз (осталось попыток: {remaining}).", ConsoleColor.DarkYellow);
            }

            PrintColor("  Ввод отменён.", ConsoleColor.DarkGray);
            return null;
        }

        private static bool Confirm(string prompt)
        {
            while (true)
            {
                Console.Write($"{prompt} [д/н]: ");
                var answer = (Console.ReadLine() ?? "").Trim().ToLowerInvariant();
                if (answer is "д" or "y" or "да" or "yes") return true;
                if (answer is "н" or "n" or "нет" or "no" or "") return false;
                PrintColor("  Введите «д» или «н».", ConsoleColor.Red);
            }
        }

        private static void PrintColor(string message, ConsoleColor color)
        {
            var prev = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ForegroundColor = prev;
        }

        private static void PrintSuccess(string message)
        {
            Console.Write("  ");
            PrintColor($"✓ {message}", ConsoleColor.Green);
            Console.WriteLine();
        }

        private static void PrintWarning(string message)
        {
            Console.Write("  ");
            PrintColor($"⚠ {message}", ConsoleColor.DarkYellow);
            Console.WriteLine();
        }


        private enum CorrectionType { DottedSubject, AnomalyTeacher }

        private class FallbackData
        {
            public List<string> SubjectNamesWithDots { get; set; } = new();
            public List<TeacherCorrection> AnomalyTeachers { get; set; } = new();
        }

        public record TeacherCorrection(string IncorrectName, string CorrectName);

        private record Correction(CorrectionType Type, string OldValue, string NewValue);
    }
}