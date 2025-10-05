using System.Text.Json.Serialization;

namespace StankinAppApi.Dto;

public class YandexUserResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("login")]
    public string Login { get; set; }

    [JsonPropertyName("client_id")]
    public string ClientId { get; set; }

    [JsonPropertyName("default_email")]
    public string DefaultEmail { get; set; }

    [JsonPropertyName("real_name")]
    public string RealName { get; set; }

    [JsonPropertyName("first_name")]
    public string FirstName { get; set; }

    [JsonPropertyName("last_name")]
    public string LastName { get; set; }

    [JsonPropertyName("is_avatar_empty")]
    public bool IsAvatarEmpty { get; set; }

    [JsonPropertyName("default_avatar_id")]
    public string DefaultAvatarId { get; set; }
}