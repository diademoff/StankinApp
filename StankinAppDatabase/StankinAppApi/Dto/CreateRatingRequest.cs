using System.Text.Json.Serialization;

namespace StankinAppApi.Dto;

public class CreateRatingRequest
{
    [JsonPropertyName("score")]
    public int Score { get; set; }
}