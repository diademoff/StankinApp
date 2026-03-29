namespace StankinAppApi.Dto;

/// <summary>
/// Универсальная обёртка ответа.
/// Alpine.js читает: response.metadata.nextWeek и response.items
/// </summary>
public record ApiResponse<T>(
    ScheduleMetadata Metadata,
    IEnumerable<T> Items
);

/// <summary>
/// Метаданные для навигации по неделям.
/// Позволяет Alpine одной строкой обновить кнопки «Вперёд/Назад».
/// </summary>
public record ScheduleMetadata(
    /// <summary>Дата начала следующей недели. "2026-04-05"</summary>
    string NextWeek,

    /// <summary>Дата начала предыдущей недели. "2026-03-22"</summary>
    string PrevWeek,

    /// <summary>Текущий отображаемый период (начало). "2026-03-29"</summary>
    string PeriodStart,

    /// <summary>Текущий отображаемый период (конец). "2026-04-04"</summary>
    string PeriodEnd,

    /// <summary>Нет данных после этой недели — Alpine скроет кнопку «Вперёд»</summary>
    bool IsLastWeek
);

/// <summary>
/// Простая обёртка для списков без навигации (группы, преподаватели, аудитории).
/// </summary>
public record ListResponse<T>(IEnumerable<T> Items);