using StankinAppApi.Dto;
using StankinAppApi.Models;

namespace StankinAppApi.Services;

public interface IRatingService
{
    Task<User> GetOrCreateUserAsync(long yandexId, string username, string firstName, string email, string photoUrl);
    Task<User> GetUserByIdAsync(int userId);
    Task<RatingResponse> CreateOrUpdateRatingAsync(int userId, int teacherId, string teacherName, int score);
    Task<RatingAggregateResponse> GetTeacherRatingsAsync(int teacherId);
    Task<int> CreateCommentAsync(int userId, int teacherId, string teacherName, string content, bool anonymous);
    Task<CommentsPageResponse> GetTeacherCommentsAsync(int teacherId, int page, int limit);
    Task<VoteResponse> VoteForCommentAsync(int userId, int commentId, int vote);
}