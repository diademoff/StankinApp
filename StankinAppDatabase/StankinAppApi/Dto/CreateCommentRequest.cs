using System.Text.Json.Serialization;

namespace StankinAppApi.Dto;

public class CreateCommentRequest
{
    [JsonPropertyName("content")]
    public string Content { get; set; }

    [JsonPropertyName("anonymous")]
    public bool Anonymous { get; set; }
}