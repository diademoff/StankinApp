using System;

namespace StankinAppApi.Models;

public class Comment
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int TeacherId { get; set; }
    public string TeacherName { get; set; }
    public bool Anonymous { get; set; }
    public string Content { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User User { get; set; }
    public int VotesSum { get; set; } // Calculated field
}