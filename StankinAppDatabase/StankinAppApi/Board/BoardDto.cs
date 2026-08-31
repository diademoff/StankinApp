namespace StankinAppApi.Board;

public record PostDto(long Id, long? ThreadId, long? ParentId, string Text, DateTime CreatedAt, DateTime UpdatedAt, bool IsDeleted, int ReportCount);

public record ThreadSummaryDto(long Id, PostDto Op, int ReplyCount, DateTime UpdatedAt, List<PostDto> LastPosts, bool IsPinned);

public record ThreadDetailDto(long Id, List<PostDto> Posts);

public record ReportDto(long Id, long? ThreadId, string Text, int ReportCount, string IpHash, DateTime CreatedAt);

public record BoardRequest(string Text, string CaptchaToken, long? ParentId, bool Sage);

public record BanRequest(string IpHash);

public static class BoardMapper
{
    private const string DeletedText = "Сообщение удалено модератором";

    public static PostDto ToDto(Post p) =>
        new(p.Id, p.ThreadId, p.ParentId, p.IsDeleted ? DeletedText : p.Text,
            p.CreatedAt, p.UpdatedAt, p.IsDeleted, p.ReportCount);
}
