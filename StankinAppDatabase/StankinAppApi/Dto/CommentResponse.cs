using System.Text.Json.Serialization;

namespace StankinAppApi.Dto;

public class CommentResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
}