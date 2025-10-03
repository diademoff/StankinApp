using Newtonsoft.Json.Linq;
using NodaTime;
using NodaTime.Text;
using StankinAppCore;
using System.Text.RegularExpressions;

namespace StankinAppDatabase
{
    public class ScheduleJsonReader(int currentYear, Func<ErrorParsingInfo, Course[]> parseError)
    {
        private static readonly HashSet<string> AllowedLessonTypes = ["лекции", "семинар", "лабораторные занятия", "Лекция", "Семинар", "Лабораторная"];
        int currentYear = currentYear;

        readonly Func<ErrorParsingInfo, Course[]> parseError = parseError;

        public Schedule GetSchedule(string groupName, string fileJson)
        {
            JObject data = JObject.Parse(fileJson);
            List<Course> courses = new List<Course>();

            foreach (var day in data.Properties())
            {
                foreach (var timeInterval in (day.Value.ToObject<JObject>() ?? throw new NullReferenceException($"{nameof(day)} timeInterval is null "))
                                                .Properties())
                {
                    string[] timeParts = timeInterval.Name.Split('-');
                    if (timeParts.Length != 2)
                    {
                        continue;
                    }

                    var str_startTime = timeParts[0].Trim();
                    var str_endTime = timeParts[1].Trim();

                    // Время хранится как начало LocalTime и продолжительность Period
                    string[] startTimeParts = str_startTime.Split(':');
                    string[] endTimeParts = str_endTime.Split(':');

                    if (startTimeParts.Length != 2 || endTimeParts.Length != 2)
                    {
                        throw new FormatException($"Invalid time format: {str_startTime} or {str_endTime}");
                    }

                    int startHour = int.Parse(startTimeParts[0]);
                    int startMinute = int.Parse(startTimeParts[1]);
                    int endHour = int.Parse(endTimeParts[0]);
                    int endMinute = int.Parse(endTimeParts[1]);

                    LocalTime startTime = new LocalTime(startHour, startMinute);
                    LocalTime endTime = new LocalTime(endHour, endMinute);

                    Period duration = endTime - startTime;

                    List<string>? list = timeInterval.Value.ToObject<List<string>>();
                    for (int i = 0; i < list?.Count; i++)
                    {
                        string? lessonLine = list[i];
                        List<Course> parsedLessons = ParseLessons(lessonLine, startTime, duration, groupName);
                        courses.AddRange(parsedLessons);
                    }
                }
            }

            return new Schedule(groupName, courses);
        }

        /// <summary>
        /// Формат: subject. teacher. type. (subgroup.)? cabinet. [dates]
        /// </summary>
        private static MatchCollection? GetCoursesInString(string rowString)
        {
            // шаблон для всей записи:
            var pattern = @"(?<subject>[^\.]+)\.\s*" +
                      @"(?:(?<teacher>[А-ЯЁ][^\.]+\s+[А-Я]\.(?:\s*[А-Я]\.)?)\s*\.*\s*)?" +
                      @"(?<type>[^\.]+?)\.\s*" +
                      @"(?:(?:\((?<subgroup>[^)]+)\)\.\s*)?)" +
                      @"(?<cabinet>[^\.]+?)\.\s*" +
                      @"\[(?<dates>[^\]]+)\]";

            return Regex.Matches(rowString, pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        }

        string FixSubject(string subject)
        {
            if (string.IsNullOrEmpty(subject))
                return subject;

            // Используем регулярное выражение для поиска минуса, за которым следует пробел, и заменяем на минус без пробела
            return Regex.Replace(subject, @"-\s+", "-");
        }

        public List<Course> ParseLessons(string lessonLine, LocalTime startTime, Period duration, string groupName, bool throwOnFail = false)
        {
            if (lessonLine is null)
                throw new NullReferenceException($"{nameof(lessonLine)} is null");
            // lessonLine example:
            // Интеграция информационных систем и технологий. Тихомирова В.Д. лабораторные занятия. (А). 249(б). [26.03-23.04 ч.н., 14.05]
            MatchCollection? matches = GetCoursesInString(lessonLine);

            if (matches is null) throw new NullReferenceException($"{nameof(matches)} is null");

            List<Course> entries = [];
            foreach (Match match in matches)
            {
                if (match.Success)
                {
                    var subject = match.Groups["subject"].Value.Trim();
                    subject = FixSubject(subject);
                    var teacher = match.Groups["teacher"].Success ? match.Groups["teacher"].Value.Trim() : "";
                    var type = match.Groups["type"].Value.Trim();
                    var subgroup = match.Groups["subgroup"].Success ? match.Groups["subgroup"].Value.Trim() : "";
                    var cabinet = match.Groups["cabinet"].Value.Trim()
                        .Replace("- ", "-")
                        .Replace(") ", ")");
                    var datesString = match.Groups["dates"].Value.Trim();
                    var dates = ParseSchedule(datesString, currentYear) ?? throw new ArgumentNullException();

                    var entry = new Course
                    {
                        StartTime = startTime,
                        Duration = duration,
                        GroupName = groupName,
                        Subject = subject,
                        Teacher = teacher,
                        Type = type,
                        Subgroup = subgroup,
                        Cabinet = cabinet,
                        Dates = dates
                    };

                    entries.Add(entry);
                }
            }

            // Вызвать обработчик ошибок парсинга
            bool isCorrect = entries.TrueForAll(x => !string.IsNullOrWhiteSpace(x.Subject) && x.Subject.Length > 3 && AllowedLessonTypes.Contains(x.Type));
            if (!isCorrect)
            {
                if (throwOnFail)
                    throw new Exception("Parsing failed");
                return [.. parseError(new ErrorParsingInfo()
                {
                    LineToParse = lessonLine,
                    FailedToParseCourses = entries,
                    GroupName = groupName,
                    StartTime = startTime,
                    Duration = duration
                })];
            }

            return entries;
        }

        public List<LocalDate> ParseSchedule(string datesString, int currentYear)
        {
            if (datesString.Contains(','))
            {
                string[] subDates = datesString.Split(',').Select(x => x.Trim()).ToArray();
                var dates = new List<LocalDate>();
                foreach (var date in subDates)
                {
                    dates.AddRange(ParseSchedule(date, currentYear));
                }
                return dates;
            }

            var dateSchedules = new List<LocalDate>();

            var parts = datesString.Split([','], StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                var pattern = @"^(?<start>\d{2}\.\d{2})(-(?<end>\d{2}\.\d{2}))?\s*(?<period>(ч\.н\.|к\.н\.))?$";
                var match = Regex.Match(trimmed, pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var startStr = match.Groups["start"].Value;
                    if (!LocalDatePattern.CreateWithInvariantCulture("dd.MM").Parse(startStr).TryGetValue(new LocalDate(currentYear, 1, 1), out var startDate))
                        throw new RegexMatchTimeoutException("не удалось спарсить дату start");

                    startDate = new LocalDate(currentYear, startDate.Month, startDate.Day);

                    LocalDate? endDate = null;
                    if (match.Groups["end"].Success && !string.IsNullOrEmpty(match.Groups["end"].Value))
                    {
                        var endStr = match.Groups["end"].Value;
                        if (LocalDatePattern.CreateWithInvariantCulture("dd.MM").Parse(endStr).TryGetValue(new LocalDate(currentYear, 1, 1), out var parsedEnd))
                        {
                            endDate = new LocalDate(currentYear, parsedEnd.Month, parsedEnd.Day);
                        }
                        else
                        {
                            throw new RegexMatchTimeoutException("не удалось спарсить дату end");
                        }
                    }

                    string period = "";
                    if (match.Groups["period"].Success)
                        period = match.Groups["period"].Value;

                    if (endDate is null)
                    {
                        if (string.IsNullOrWhiteSpace(period))
                            return new List<LocalDate>() { startDate };
                        else
                            throw new Exception("impossible situation endDate is null but period is not");
                    }

                    LocalDate current = startDate;
                    do
                    {
                        dateSchedules.Add(current);
                        current = period switch
                        {
                            "к.н." => current.PlusWeeks(1),
                            "ч.н." => current.PlusWeeks(2),
                            _ => current
                        };
                    }
                    while (current <= endDate);
                }
            }
            return dateSchedules;
        }
    }
}
