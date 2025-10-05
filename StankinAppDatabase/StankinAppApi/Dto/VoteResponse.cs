using System.Text.Json.Serialization;

namespace StankinAppApi.Dto;

public class VoteResponse
{
    [JsonPropertyName("commentId")]
    public int CommentId { get; set; }

    [JsonPropertyName("vote")]
    public int Vote { get; set; }
}