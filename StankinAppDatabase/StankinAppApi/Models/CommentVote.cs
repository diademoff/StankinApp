using System;

namespace StankinAppApi.Models;

public class CommentVote
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int CommentId { get; set; }
    public int Vote { get; set; } // -1 or 1
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User User { get; set; }
    public Comment Comment { get; set; }
}