using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using StankinAppApi.Dto;
using StankinAppApi.Services;
using System.Security.Claims;
using Serilog;

namespace StankinAppApi.Controllers;

[ApiController]
[Route("api/teachers")]
public class TeacherRatingController : ControllerBase
{
    private readonly IRatingService _ratingService;
    private readonly IScheduleService _scheduleService;
    private readonly ILogger<TeacherRatingController> _logger;

    public TeacherRatingController(
        IRatingService ratingService,
        IScheduleService scheduleService,
        ILogger<TeacherRatingController> logger)
    {
        _ratingService = ratingService;
        _scheduleService = scheduleService;
        _logger = logger;
    }

    [HttpPost("{teacherId}/ratings")]
    [Authorize]
    public async Task<IActionResult> CreateRating(int teacherId, [FromBody] CreateRatingRequest request)
    {
        try
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdStr == null) return Unauthorized();
            var userId = int.Parse(userIdStr);

            // Validate score
            if (request.Score < 1 || request.Score > 10)
            {
                return BadRequest(new ErrorResponse { Error = "Score must be 1-10." });
            }

            // Get teacher name from schedule service
            var teachers = _scheduleService.GetTeachers();
            var teacherName = teachers.ElementAtOrDefault(teacherId - 1); // Assuming teacherId is 1-based index

            if (string.IsNullOrEmpty(teacherName))
            {
                return NotFound(new ErrorResponse { Error = "Teacher not found" });
            }

            var result = await _ratingService.CreateOrUpdateRatingAsync(userId, teacherId, teacherName, request.Score);

            _logger.LogInformation("User {UserId} rated teacher {TeacherId} with score {Score}",
                userId, teacherId, request.Score);

            return Ok(new ApiResponse<RatingResponse> { Data = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating rating for teacher {TeacherId}", teacherId);
            return StatusCode(500, new ErrorResponse { Error = "Internal server error" });
        }
    }

    [HttpGet("{teacherId}/ratings")]
    public async Task<IActionResult> GetRatings(int teacherId)
    {
        try
        {
            var result = await _ratingService.GetTeacherRatingsAsync(teacherId);
            return Ok(new ApiResponse<RatingAggregateResponse> { Data = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting ratings for teacher {TeacherId}", teacherId);
            return StatusCode(500, new ErrorResponse { Error = "Internal server error" });
        }
    }

    [HttpPost("{teacherId}/comments")]
    [Authorize]
    public async Task<IActionResult> CreateComment(int teacherId, [FromBody] CreateCommentRequest request)
    {
        try
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdStr == null) return Unauthorized();
            var userId = int.Parse(userIdStr);

            // Validate content
            if (string.IsNullOrWhiteSpace(request.Content))
            {
                return BadRequest(new ErrorResponse { Error = "Comment content cannot be empty" });
            }

            if (request.Content.Length > 5000)
            {
                return BadRequest(new ErrorResponse { Error = "Comment content cannot exceed 5000 characters" });
            }

            // Get teacher name
            var teachers = _scheduleService.GetTeachers();
            var teacherName = teachers.ElementAtOrDefault(teacherId - 1);

            if (string.IsNullOrEmpty(teacherName))
            {
                return NotFound(new ErrorResponse { Error = "Teacher not found" });
            }

            var commentId = await _ratingService.CreateCommentAsync(
                userId, teacherId, teacherName, request.Content, request.Anonymous);

            _logger.LogInformation("User {UserId} created comment {CommentId} for teacher {TeacherId}",
                userId, commentId, teacherId);

            return Ok(new ApiResponse<CommentResponse>
            {
                Data = new CommentResponse { Id = commentId }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating comment for teacher {TeacherId}", teacherId);
            return StatusCode(500, new ErrorResponse { Error = "Internal server error" });
        }
    }

    [HttpGet("{teacherId}/comments")]
    public async Task<IActionResult> GetComments(int teacherId, [FromQuery] int page = 1, [FromQuery] int limit = 20)
    {
        try
        {
            var result = await _ratingService.GetTeacherCommentsAsync(teacherId, page, limit);
            return Ok(new ApiResponse<CommentsPageResponse> { Data = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting comments for teacher {TeacherId}", teacherId);
            return StatusCode(500, new ErrorResponse { Error = "Internal server error" });
        }
    }
}
