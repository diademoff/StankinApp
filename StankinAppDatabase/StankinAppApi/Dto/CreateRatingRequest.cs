using System.Text.Json.Serialization;

namespace StankinAppApi.Dto;

public class CreateRatingRequest
{
    [JsonPropertyName("teacherName")]
    public string TeacherName { get; set; } = null!;

    [JsonPropertyName("score")]
    public int Score { get; set; }
}