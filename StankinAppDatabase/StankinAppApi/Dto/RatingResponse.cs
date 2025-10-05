using System.Text.Json.Serialization;

namespace StankinAppApi.Dto;

public class RatingResponse
{
    [JsonPropertyName("teacherId")]
    public int TeacherId { get; set; }

    [JsonPropertyName("score")]
    public int Score { get; set; }
}