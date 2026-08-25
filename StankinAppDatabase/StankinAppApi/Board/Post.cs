namespace StankinAppApi.Board;

public class Post
{
    public long Id { get; set; }
    public long? ThreadId { get; set; }
    public long? ParentId { get; set; }
    public string Text { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public int ReportCount { get; set; }
    public string IpHash { get; set; } = "";
}
