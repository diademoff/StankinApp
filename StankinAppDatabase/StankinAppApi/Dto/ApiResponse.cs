using System.Text.Json.Serialization;

namespace StankinAppApi.Dto;

public class ApiResponse<T>
{
    [JsonPropertyName("data")]
    public T Data { get; set; }
}