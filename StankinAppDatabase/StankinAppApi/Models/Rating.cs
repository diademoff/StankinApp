using System;

namespace StankinAppApi.Models;

public class Rating
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int TeacherId { get; set; }
    public string TeacherName { get; set; }
    public int Score { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User User { get; set; }
}