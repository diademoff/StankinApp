namespace StankinAppApi.Dto;

public record ListResponse<T>(IEnumerable<T> Items);
