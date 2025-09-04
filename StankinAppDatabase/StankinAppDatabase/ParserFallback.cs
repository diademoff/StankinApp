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
                courses = _reader.ParseLessons(
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
                courses = _reader.ParseLessons(
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

                courses = _reader.ParseLessons(
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
            DisplayErrorInfo(err);
            var correction = GetUserCorrection(err);
            ApplyCorrection(correction, err);
            SaveFallbacks();

            return ParseFallbackFunc(err);
        }

        private void DisplayErrorInfo(ErrorParsingInfo err)
        {
            Console.WriteLine($"Ошибка при парсинге строки. Группа: {err.GroupName}\n");
            Console.WriteLine(err.LineToParse);
            Console.WriteLine("\nВарианты парсинга:");

            for (int i = 0; i < err.FailedToParseCourses.Count; i++)
            {
                var course = err.FailedToParseCourses[i];
                Console.WriteLine($"{i + 1}. Предмет: {course.Subject}\n   Преподаватель: {course.Teacher}\n   Тип: {course.Type}\n");
            }
        }

        private Correction GetUserCorrection(ErrorParsingInfo err)
        {
            Console.WriteLine("Где ошибка парсинга?");
            Console.Write("Номер строки: ");
            int lineNumber = int.Parse(Console.ReadLine(), NumberStyles.Integer, CultureInfo.InvariantCulture);

            Console.WriteLine("Тип ошибки:\n1. Предмет с точкой\n2. Преподаватель");
            Console.Write("Тип ошибки: ");
            int errorType = int.Parse(Console.ReadLine(), NumberStyles.Integer, CultureInfo.InvariantCulture);

            var selectedCourse = err.FailedToParseCourses[lineNumber - 1];

            Console.WriteLine("Введите старое значение: ");
            string oldValue = Console.ReadLine();

            Console.Write($"Новое значение для \"{oldValue}\": ");
            string newValue = Console.ReadLine();

            return new Correction(errorType, oldValue, newValue);
        }

        private void ApplyCorrection(Correction correction, ErrorParsingInfo err)
        {
            switch (correction.ErrorType)
            {
                case 1:
                    SubjectNamesWithDots.Add(correction.NewValue);
                    Console.WriteLine("Добавлен предмет с точкой");
                    break;
                case 2:
                    AnomalyTeachers.Add(new TeacherCorrection(correction.OldValue, correction.NewValue));
                    Console.WriteLine("Добавлена коррекция преподавателя");
                    break;
            }
        }

        private class FallbackData
        {
            public List<string> SubjectNamesWithDots { get; set; }
            public List<TeacherCorrection> AnomalyTeachers { get; set; }
        }

        public record TeacherCorrection(string IncorrectName, string CorrectName);
        private record Correction(int ErrorType, string OldValue, string NewValue);
    }
}