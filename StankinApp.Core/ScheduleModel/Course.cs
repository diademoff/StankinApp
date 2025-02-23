using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace StankinApp.Core.ScheduleModel
{
    public class Course
    {
        public PairTime Time { get; set; }
        public string Subject { get; private set; }
        public string Teacher { get; private set; } // может быть null
        string _type;
        public string Type
        {
            get
            {
                if (_type == "лабораторные занятия")
                    return "Л.Р.";
                return _type;
            }
        }

        public bool AnySubgroup => !string.IsNullOrWhiteSpace(Subgroup);
        public string Subgroup { get; private set; } // может быть null
        public string Cabinet { get; private set; }
        public bool IsNow
        {
            get
            {
                var n = DateTime.Now;
                n = new DateTime(2025, 2, 24, 9, 0, 0);

                if (DateSchedules.Where(x => x.Day == n.Day && x.Month == n.Month).Any())
                {
                    DateTime tb = new DateTime(n.Year, n.Month, n.Day, Time.TimeBegin.Hour, Time.TimeBegin.Minute, 0);
                    DateTime te = new DateTime(n.Year, n.Month, n.Day, Time.TimeEnd.Hour, Time.TimeEnd.Minute, 0);
                    if (n >= tb)
                    {
                        if (n <= te)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
        }
        public double TimeProgress
        {
            get
            {
                var n = DateTime.Now;
                n = new DateTime(2025, 2, 24, 10, 0, 0);
                if (!IsNow)
                    return 0;
                DateTime tb = new DateTime(n.Year, n.Month, n.Day, Time.TimeBegin.Hour, Time.TimeBegin.Minute, 0);
                DateTime te = new DateTime(n.Year, n.Month, n.Day, Time.TimeEnd.Hour, Time.TimeEnd.Minute, 0);

                return (n - tb).TotalSeconds / (te - tb).TotalSeconds * 100;
            }
        }

        public List<DateTime> DateSchedules { get; set; }

        public Course(string subject, string teacher, string type, string subgroup, string cabinet, PairTime time, string dateSchedules) : this(subject, teacher, type, subgroup, cabinet, dateSchedules, time, DateTime.Now.Year)
        { 
        }

        public Course(string subject, string teacher, string type, string subgroup, string cabinet, string dateSchedules, PairTime time, int year) 
        {
            Subject = subject ?? throw new ArgumentNullException(nameof(subject));
            Teacher = teacher;
            _type = type ?? throw new ArgumentNullException(nameof(type));
            Subgroup = subgroup;
            Cabinet = cabinet ?? throw new ArgumentNullException(nameof(cabinet));
            Time = time;
            DateSchedules = ParseDates(dateSchedules, new DateTime(year, 1, 1));
        }

        public static List<Course> Parse(string input, PairTime pairTime)
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

                    var entry = new Course(subject, teacher, type, subgroup, cabinet, pairTime, datesString);

                    entries.Add(entry);
                }
            }
            return entries;
        }

        public bool IsInDate(DateTime date)
        {
            foreach (var courseDates in DateSchedules)
            {
                if (courseDates.Day == date.Day && courseDates.Month == date.Month)
                    return true;
            }
            return false;
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
            if (datesString.Contains(','))
            {
                // Например
                // 13.02-24.04 к.н., 15.05
                string[] subDates = datesString.Split(',').Select(x => x.Trim()).ToArray();
                var dates = new List<DateTime>();
                foreach (var date in subDates)
                {
                    dates.AddRange(ParseDates(date, year));
                }
                return dates;
            }

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
