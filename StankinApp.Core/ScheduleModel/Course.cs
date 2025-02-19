using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace StankinApp.Core.ScheduleModel
{
    public class Course
    {
        public string Subject { get; private set; }
        public string Teacher { get; private set; } // может быть null
        public string Type { get; private set; }
        public string Subgroup { get; private set; } // может быть null
        public string Cabinet { get; private set; }

        public List<DateTime> DateSchedules { get; set; }

        public Course(string subject, string teacher, string type, string subgroup, string cabinet, string dateSchedules) : this(subject, teacher, type, subgroup, cabinet, dateSchedules, DateTime.Now.Year)
        { 
        }

        public Course(string subject, string teacher, string type, string subgroup, string cabinet, string dateSchedules, int year) 
        {
            Subject = subject ?? throw new ArgumentNullException(nameof(subject));
            Teacher = teacher;
            Type = type ?? throw new ArgumentNullException(nameof(type));
            Subgroup = subgroup;
            Cabinet = cabinet ?? throw new ArgumentNullException(nameof(cabinet));
            DateSchedules = ParseDates(dateSchedules, new DateTime(year, 1, 1));
        }

        public static List<Course> Parse(string input)
        {
            var entries = new List<Course>();
            var matches = GetCoursesInString(input) ?? throw new Exception("empty course matches");

            foreach (Match match in matches)
            {
                if (match.Success)
                {
                    var subject = match.Groups["subject"].Value.Trim();
                    var teacher = match.Groups["teacher"].Success ? match.Groups["teacher"].Value.Trim() : "";
                    var type = match.Groups["type"].Value.Trim();
                    var subgroup = match.Groups["subgroup"].Success ? match.Groups["subgroup"].Value.Trim() : "";
                    var cabinet = match.Groups["cabinet"].Value.Trim();
                    var datesString = match.Groups["dates"].Value.Trim();

                    var entry = new Course(subject, teacher, type, subgroup, cabinet, datesString);

                    entries.Add(entry);
                }
            }
            return entries;
        }

        /// <summary>
        /// Формат: subject. teacher. type. (subgroup.)? cabinet. [dates]
        /// </summary>
        private static MatchCollection? GetCoursesInString(string rowString)
        {
            // шаблон для всей записи: 
            var pattern = @"(?<subject>[^\.]+)\.\s*" +
                          @"(?:(?<teacher>[А-ЯЁ][^\.]+\s+[А-Я]\.[А-Я]\.)\s*)?" +
                          @"(?<type>[^\.]+?)\.\s*" +
                          @"(?:(?:\((?<subgroup>[^)]+)\)\.\s*)?)" +
                          @"(?<cabinet>[^\.]+?)\.\s*" +
                          @"\[(?<dates>[^\]]+)\]";

             return Regex.Matches(rowString, pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        }

        List<DateTime> ParseDates(string datesString, DateTime year)
        {
            var dateSchedules = new List<DateTime>();

            // делим по запятым, чтобы поддержать несколько дат/диапазонов
            var parts = datesString.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                // шаблон:
                // start: dd.MM, end: dd.MM, повтор: ч.н. или к.н.
                var pattern = @"^(?<start>\d{2}\.\d{2})(-(?<end>\d{2}\.\d{2}))?\s*(?<period>(ч\.н\.|к\.н\.))?$";
                var match = Regex.Match(trimmed, pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var startStr = match.Groups["start"].Value;
                    if (!DateTime.TryParseExact(startStr, "dd.MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime startDate))
                        throw new RegexMatchTimeoutException("не удалось спарсить дату start");

                    // ставим текущий год
                    startDate = new DateTime(year.Year, startDate.Month, startDate.Day);

                    DateTime? endDate = null;
                    if (match.Groups["end"].Success && !string.IsNullOrEmpty(match.Groups["end"].Value))
                    {
                        var endStr = match.Groups["end"].Value;
                        if (DateTime.TryParseExact(endStr, "dd.MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedEnd))
                        {
                            endDate = new DateTime(year.Year, parsedEnd.Month, parsedEnd.Day);
                        }
                        else
                        {
                            throw new RegexMatchTimeoutException("не удалось спарсить дату end (datetime)");
                        }
                    }

                    string period = "";
                    if (match.Groups["period"].Success)
                         period = match.Groups["period"].Value;

                    if (endDate is null)
                    {
                        if (string.IsNullOrWhiteSpace(period))
                            return new List<DateTime>() { startDate };
                        else
                            throw new Exception("impossible situation endDate is null but period is not");
                    }

                    DateTime current = startDate;
                    do
                    {
                        dateSchedules.Add(current);
                        current += period == "к.н." ? TimeSpan.FromDays(7) : TimeSpan.Zero;
                        current += period == "ч.н." ? TimeSpan.FromDays(14) : TimeSpan.Zero;
                    }
                    while (current <= endDate);
                }
            }
            return dateSchedules;
        }
    }
}
