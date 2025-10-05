using System;

namespace StankinAppApi.Models;

public class User
{
    public int Id { get; set; }
    public long YandexId { get; set; }
    public string FirstName { get; set; }
    public string Username { get; set; }
    public string PhotoUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}