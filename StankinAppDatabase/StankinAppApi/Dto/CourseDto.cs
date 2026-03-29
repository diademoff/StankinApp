namespace StankinAppApi.Dto;

/// <summary>
/// Плоский DTO одного занятия. Все поля готовы для Alpine.js:
/// — даты как ISO 8601 строки
/// — тип занятия как читаемая строка ("Лекция", "Семинар", ...)
/// — уникальный id для x-for :key
/// </summary>
public record CourseDto(
    /// <summary>Уникальный ключ для Alpine x-for :key. Формат: "groupName_date_startTime_subgroup"</summary>
    string Id,

    /// <summary>Дата занятия без времени. Например: "2026-03-29"</summary>
    string Date,

    /// <summary>Время начала. Например: "08:30"</summary>
    string StartTime,

    /// <summary>Время окончания. Например: "10:00"</summary>
    string EndTime,

    /// <summary>Длительность в минутах</summary>
    int DurationMinutes,

    string GroupName,
    string Subject,
    string Teacher,

    /// <summary>"Лекция" | "Семинар" | "Лабораторная работа"</summary>
    string Type,

    string Subgroup,
    string Cabinet,
    int SequencePosition,
    int SequenceLength
);