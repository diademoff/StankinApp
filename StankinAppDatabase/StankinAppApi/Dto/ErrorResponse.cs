using System.Text.Json.Serialization;

namespace StankinAppApi.Dto;

public class ErrorResponse
{
    [JsonPropertyName("error")]
    public string Error { get; set; }

    [JsonPropertyName("details")]
    public object Details { get; set; }
}