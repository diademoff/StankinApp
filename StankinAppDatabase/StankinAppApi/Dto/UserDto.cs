using System.Text.Json.Serialization;

namespace StankinAppApi.Dto;

public class UserDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("firstName")]
    public string FirstName { get; set; }

    [JsonPropertyName("username")]
    public string Username { get; set; }

    [JsonPropertyName("photoUrl")]
    public string PhotoUrl { get; set; }
}