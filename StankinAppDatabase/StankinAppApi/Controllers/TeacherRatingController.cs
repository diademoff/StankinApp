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

    [HttpPost("comment")]
    [Authorize]
    public async Task<IActionResult> CreateComment([FromBody] CreateCommentRequest request)
    {
        try
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdStr == null) return Unauthorized();
            var userId = int.Parse(userIdStr);
            string name = request.TeacherName;

            // Validate content
            if (string.IsNullOrWhiteSpace(request.Content))
            {
                return BadRequest(new ErrorResponse { Error = "Comment content cannot be empty" });
            }

            if (request.Content.Length > 5000)
            {
                return BadRequest(new ErrorResponse { Error = "Comment content cannot exceed 5000 characters" });
            }

            if (string.IsNullOrEmpty(name))
            {
                return NotFound(new ErrorResponse { Error = "Teacher not found" });
            }

            var commentId = await _ratingService.CreateCommentAsync(
                userId, name, request.Content, request.Anonymous);

            _logger.LogInformation("User {UserId} created comment {CommentId} for teacher {TeacherId}",
                userId, commentId, name);

            return Ok(new ApiResponse<CommentResponse>
            {
                Data = new CommentResponse { Id = commentId }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating comment for teacher {TeacherId}", request.TeacherName);
            return StatusCode(500, new ErrorResponse { Error = "Internal server error" });
        }
    }

    [HttpGet("get-comments")]
    public async Task<IActionResult> GetComments(string name, [FromQuery] int page = 1, [FromQuery] int limit = 20)
    {
        try
        {
            var result = await _ratingService.GetTeacherCommentsAsync(name, page, limit);
            return Ok(new ApiResponse<CommentsPageResponse> { Data = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting comments for teacher {TeacherId}", name);
            return StatusCode(500, new ErrorResponse { Error = "Internal server error" });
        }
    }


    [HttpPost("vote-rating")]
    [Authorize]
    public async Task<IActionResult> CreateRatingByName([FromBody] CreateRatingRequest request)
    {
        try
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdStr == null) return Unauthorized();
            var userId = int.Parse(userIdStr);

            if (string.IsNullOrWhiteSpace(request.TeacherName))
                return BadRequest(new ErrorResponse { Error = "Teacher name is required" });

            if (request.Score < 1 || request.Score > 10)
                return BadRequest(new ErrorResponse { Error = "Score must be 1-10." });

            // 🔑 Валидация: есть ли такой преподаватель в расписании?
            var validTeachers = _scheduleService.GetTeachers();
            if (!validTeachers.Contains(request.TeacherName, StringComparer.OrdinalIgnoreCase))
                return NotFound(new ErrorResponse { Error = "Преподаватель не найден в расписании" });

            var result = await _ratingService.CreateOrUpdateRatingAsync(userId, request.TeacherName, request.Score);
            _logger.LogInformation("User {UserId} rated teacher '{TeacherName}' with score {Score}", userId, request.TeacherName, request.Score);
            return Ok(new ApiResponse<RatingResponse> { Data = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating rating for teacher '{TeacherName}'", request?.TeacherName);
            return StatusCode(500, new ErrorResponse { Error = "Internal server error" });
        }
    }

    [HttpGet("get-teacher-rating")]
    public async Task<IActionResult> GetRatingsByName([FromQuery] string name)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(name))
                return BadRequest(new ErrorResponse { Error = "Missing 'name' parameter" });

            var result = await _ratingService.GetTeacherRatingsAsync(name);
            return Ok(new ApiResponse<RatingAggregateResponse> { Data = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting ratings for teacher '{TeacherName}'", name);
            return StatusCode(500, new ErrorResponse { Error = "Internal server error" });
        }
    }

    [HttpGet("get-user-rating")]
    [Authorize]
    public async Task<IActionResult> GetUserRatingForTeacher([FromQuery] string name)
    {
        try
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdStr == null) return Unauthorized();
            var userId = int.Parse(userIdStr);

            var rating = await _ratingService.GetUserRatingAsync(userId, name);
            return Ok(new ApiResponse<UserRatingResponse>
            {
                Data = new UserRatingResponse { Score = rating }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user rating for teacher {TeacherName}", name);
            return StatusCode(500, new ErrorResponse { Error = "Internal server error" });
        }
    }
}
