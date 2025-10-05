using StankinAppApi.Dto;
using StankinAppApi.Models;

namespace StankinAppApi.Services;

public interface IRatingService
{
    Task<User> GetOrCreateUserAsync(long yandexId, string username, string firstName, string email, string photoUrl);
    Task<User> GetUserByIdAsync(int userId);
    Task<RatingResponse> CreateOrUpdateRatingAsync(int userId, string teacherName, int score);
    Task<RatingAggregateResponse> GetTeacherRatingsAsync(string teacherName);
    Task<int> CreateCommentAsync(int userId, string teacherName, string content, bool anonymous);
    Task<CommentsPageResponse> GetTeacherCommentsAsync(string teacherName, int page, int limit);
    Task<VoteResponse> VoteForCommentAsync(int userId, int commentId, int vote);
}