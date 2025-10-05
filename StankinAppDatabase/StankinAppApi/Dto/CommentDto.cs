using System.Text.Json.Serialization;

namespace StankinAppApi.Dto;

public class CommentDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("user")]
    public UserDto User { get; set; }

    [JsonPropertyName("anonymous")]
    public bool Anonymous { get; set; }

    [JsonPropertyName("content")]
    public string Content { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("votes")]
    public int Votes { get; set; }
}