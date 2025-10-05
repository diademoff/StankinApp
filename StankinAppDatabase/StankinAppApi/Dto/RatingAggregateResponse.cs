using System.Text.Json.Serialization;

namespace StankinAppApi.Dto;

public class RatingAggregateResponse
{
    [JsonPropertyName("teacherName")]
    public string TeacherName { get; set; } 

    [JsonPropertyName("averageScore")]
    public double AverageScore { get; set; }

    [JsonPropertyName("ratingsCount")]
    public int RatingsCount { get; set; }
}