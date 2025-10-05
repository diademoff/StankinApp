using System.Text.Json.Serialization;

namespace StankinAppApi.Dto;

public class CreateCommentRequest
{
    [JsonPropertyName("teacherName")]
    public string TeacherName { get; set; } = null!;

    [JsonPropertyName("content")]
    public string Content { get; set; } = null!;

    [JsonPropertyName("anonymous")]
    public bool Anonymous { get; set; }
}