using StankinApp.Core.ScheduleModel;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace StankinApp.Core
{
    public class ScheduleJsonReader
    {
        public static Schedule GetSchedule(string jsonString)
        {
            // десериализуем json как словарь: ключ – день недели, значение – словарь с тайм-слотами и массивами курсов
            var rawSchedule = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string[]>>>(jsonString);
            if (rawSchedule == null)
                throw new Exception("не удалось распарсить json");

            var schedule = new Schedule(new List<DaySchedule>());

            // мапим каждый день и его тайм-слоты в структуру
            foreach (var dayEntry in rawSchedule)
            {
                var daySchedule = new DaySchedule(dayEntry.Key);

                foreach (var timeSlotEntry in dayEntry.Value)
                {
                    var timeSlot = new TimeSlot(timeSlotEntry.Key);

                    // тупо сохраняем всю строку как название курса
                    foreach (var courseStr in timeSlotEntry.Value)
                        timeSlot.Courses.AddRange(Course.Parse(courseStr));

                    daySchedule.TimeSlots.Add(timeSlot);
                }
                schedule.Days.Add(daySchedule);
            }

            return schedule;
        }
    }
}
