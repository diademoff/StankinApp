using System.Text.Json.Serialization;

namespace StankinAppApi.Dto;

public class CommentsPageResponse
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("limit")]
    public int Limit { get; set; }

    [JsonPropertyName("comments")]
    public List<CommentDto> Comments { get; set; }
}