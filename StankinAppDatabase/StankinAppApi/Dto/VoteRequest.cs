using System.Text.Json.Serialization;

namespace StankinAppApi.Dto;

public class VoteRequest
{
    [JsonPropertyName("vote")]
    public int Vote { get; set; }
}