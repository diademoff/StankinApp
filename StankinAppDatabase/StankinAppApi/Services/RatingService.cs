using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using StankinAppApi.Dto;
using StankinAppApi.Models;

namespace StankinAppApi.Services;

public class RatingService : IRatingService
{
    private readonly string _connectionString;
    private readonly ILogger<RatingService> _logger;

    public RatingService(IConfiguration configuration, ILogger<RatingService> logger)
    {
        _connectionString = configuration.GetConnectionString("PostgreSQL")
            ?? throw new InvalidOperationException("PostgreSQL connection string not configured");
        _logger = logger;
    }

    private IDbConnection CreateConnection() => new NpgsqlConnection(_connectionString);

    public async Task<User> GetOrCreateUserAsync(long yandexId, string username, string firstName, string email, string photoUrl)
    {
        using var connection = CreateConnection();
        var existingUser = await connection.QueryFirstOrDefaultAsync<User>(
            @"SELECT * FROM ""Users"" WHERE ""YandexId"" = @YandexId",
            new { YandexId = yandexId }
        );

        if (existingUser != null)
        {
            await connection.ExecuteAsync(
                @"UPDATE ""Users""
                  SET ""FirstName"" = @FirstName,
                      ""Username"" = @Username,
                      ""PhotoUrl"" = @PhotoUrl
                  WHERE ""Id"" = @Id",
                new { existingUser.Id, FirstName = firstName, Username = username, PhotoUrl = photoUrl }
            );
            existingUser.FirstName = firstName;
            existingUser.Username = username;
            existingUser.PhotoUrl = photoUrl;
            return existingUser;
        }

        var newUserId = await connection.QuerySingleAsync<int>(
            @"INSERT INTO ""Users"" (""YandexId"", ""FirstName"", ""Username"", ""PhotoUrl"")
              VALUES (@YandexId, @FirstName, @Username, @PhotoUrl)
              RETURNING ""Id""",
            new { YandexId = yandexId, FirstName = firstName, Username = username, PhotoUrl = photoUrl }
        );

        return await connection.QuerySingleAsync<User>(
            @"SELECT * FROM ""Users"" WHERE ""Id"" = @Id",
            new { Id = newUserId }
        );
    }

    public async Task<User> GetUserByIdAsync(int userId)
    {
        using var connection = CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<User>(
            @"SELECT * FROM ""Users"" WHERE ""Id"" = @Id",
            new { Id = userId }
        );
    }

    public async Task<RatingResponse> CreateOrUpdateRatingAsync(int userId, int teacherId, string teacherName, int score)
    {
        if (score < 1 || score > 10)
        {
            throw new ArgumentException("Score must be between 1 and 10");
        }

        using var connection = CreateConnection();

        // Check if rating exists
        var existingRating = await connection.QueryFirstOrDefaultAsync<Rating>(
            @"SELECT * FROM ""Ratings""
              WHERE ""UserId"" = @UserId AND ""TeacherId"" = @TeacherId",
            new { UserId = userId, TeacherId = teacherId }
        );

        if (existingRating != null)
        {
            // Update existing rating
            await connection.ExecuteAsync(
                @"UPDATE ""Ratings""
                  SET ""Score"" = @Score, ""TeacherName"" = @TeacherName
                  WHERE ""Id"" = @Id",
                new { Id = existingRating.Id, Score = score, TeacherName = teacherName }
            );
        }
        else
        {
            // Create new rating
            await connection.ExecuteAsync(
                @"INSERT INTO ""Ratings"" (""UserId"", ""TeacherId"", ""TeacherName"", ""Score"")
                  VALUES (@UserId, @TeacherId, @TeacherName, @Score)",
                new { UserId = userId, TeacherId = teacherId, TeacherName = teacherName, Score = score }
            );
        }

        return new RatingResponse { TeacherId = teacherId, Score = score };
    }

    public async Task<RatingAggregateResponse> GetTeacherRatingsAsync(int teacherId)
    {
        using var connection = CreateConnection();

        var result = await connection.QueryFirstOrDefaultAsync<dynamic>(
            @"SELECT
                COUNT(*) as count,
                AVG(CAST(""Score"" AS DECIMAL)) as average
              FROM ""Ratings""
              WHERE ""TeacherId"" = @TeacherId",
            new { TeacherId = teacherId }
        );

        return new RatingAggregateResponse
        {
            TeacherId = teacherId,
            AverageScore = result?.average ?? 0.0,
            RatingsCount = (int)(result?.count ?? 0)
        };
    }

    public async Task<int> CreateCommentAsync(int userId, int teacherId, string teacherName, string content, bool anonymous)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Comment content cannot be empty");
        }

        if (content.Length > 5000)
        {
            throw new ArgumentException("Comment content cannot exceed 5000 characters");
        }

        using var connection = CreateConnection();

        var commentId = await connection.QuerySingleAsync<int>(
            @"INSERT INTO ""Comments"" (""UserId"", ""TeacherId"", ""TeacherName"", ""Content"", ""Anonymous"")
              VALUES (@UserId, @TeacherId, @TeacherName, @Content, @Anonymous)
              RETURNING ""Id""",
            new
            {
                UserId = userId,
                TeacherId = teacherId,
                TeacherName = teacherName,
                Content = content,
                Anonymous = anonymous
            }
        );

        return commentId;
    }

    public async Task<CommentsPageResponse> GetTeacherCommentsAsync(int teacherId, int page, int limit)
    {
        if (page < 1) page = 1;
        if (limit < 1 || limit > 100) limit = 20;

        using var connection = CreateConnection();

        // Get total count
        var total = await connection.QuerySingleAsync<int>(
            @"SELECT COUNT(*) FROM ""Comments""
              WHERE ""TeacherId"" = @TeacherId AND ""IsDeleted"" = false",
            new { TeacherId = teacherId }
        );

        // Get paginated comments with votes
        var offset = (page - 1) * limit;
        var comments = await connection.QueryAsync<dynamic>(
            @"SELECT
                c.""Id"",
                c.""UserId"",
                c.""Content"",
                c.""Anonymous"",
                c.""CreatedAt"",
                u.""FirstName"",
                u.""Username"",
                u.""PhotoUrl"",
                COALESCE(SUM(v.""Vote""), 0) as VotesSum
              FROM ""Comments"" c
              INNER JOIN ""Users"" u ON c.""UserId"" = u.""Id""
              LEFT JOIN ""CommentVotes"" v ON c.""Id"" = v.""CommentId""
              WHERE c.""TeacherId"" = @TeacherId AND c.""IsDeleted"" = false
              GROUP BY c.""Id"", c.""UserId"", c.""Content"", c.""Anonymous"", c.""CreatedAt"",
                       u.""FirstName"", u.""Username"", u.""PhotoUrl""
              ORDER BY c.""CreatedAt"" DESC
              LIMIT @Limit OFFSET @Offset",
            new { TeacherId = teacherId, Limit = limit, Offset = offset }
        );

        var commentDtos = new List<CommentDto>();
        foreach (var comment in comments)
        {
            commentDtos.Add(new CommentDto
            {
                Id = comment.Id,
                User = comment.Anonymous ? null : new UserDto
                {
                    Id = comment.UserId,
                    FirstName = comment.FirstName,
                    Username = comment.Username,
                    PhotoUrl = comment.PhotoUrl
                },
                Anonymous = comment.Anonymous,
                Content = comment.Content,
                CreatedAt = comment.CreatedAt,
                Votes = (int)comment.VotesSum
            });
        }

        return new CommentsPageResponse
        {
            Total = total,
            Page = page,
            Limit = limit,
            Comments = commentDtos
        };
    }

    public async Task<VoteResponse> VoteForCommentAsync(int userId, int commentId, int vote)
    {
        if (vote != -1 && vote != 1)
        {
            throw new ArgumentException("Vote must be -1 or 1");
        }

        using var connection = CreateConnection();

        // Check if comment exists
        var commentExists = await connection.QuerySingleAsync<bool>(
            @"SELECT EXISTS(SELECT 1 FROM ""Comments"" WHERE ""Id"" = @CommentId AND ""IsDeleted"" = false)",
            new { CommentId = commentId }
        );

        if (!commentExists)
        {
            throw new ArgumentException("Comment not found");
        }

        // Check if user already voted
        var existingVote = await connection.QueryFirstOrDefaultAsync<CommentVote>(
            @"SELECT * FROM ""CommentVotes""
              WHERE ""UserId"" = @UserId AND ""CommentId"" = @CommentId",
            new { UserId = userId, CommentId = commentId }
        );

        if (existingVote != null)
        {
            if (existingVote.Vote == vote)
            {
                throw new InvalidOperationException("Already voted with the same value");
            }

            // Update existing vote
            await connection.ExecuteAsync(
                @"UPDATE ""CommentVotes""
                  SET ""Vote"" = @Vote
                  WHERE ""Id"" = @Id",
                new { Id = existingVote.Id, Vote = vote }
            );
        }
        else
        {
            // Create new vote
            await connection.ExecuteAsync(
                @"INSERT INTO ""CommentVotes"" (""UserId"", ""CommentId"", ""Vote"")
                  VALUES (@UserId, @CommentId, @Vote)",
                new { UserId = userId, CommentId = commentId, Vote = vote }
            );
        }

        return new VoteResponse { CommentId = commentId, Vote = vote };
    }
}