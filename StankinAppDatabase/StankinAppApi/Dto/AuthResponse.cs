using System.Text.Json.Serialization;

namespace StankinAppApi.Dto;

public class AuthResponse
{
    [JsonPropertyName("jwt")]
    public string Jwt { get; set; }

    [JsonPropertyName("token")]
    public string Token { get; set; }

    [JsonPropertyName("user")]
    public UserDto User { get; set; }
}