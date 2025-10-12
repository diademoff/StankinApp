using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using StankinAppApi.Dto;
using StankinAppApi.Services;
using System.Security.Claims;
using Serilog;

namespace StankinAppApi.Controllers;

[ApiController]
[Route("api/comments")]
public class CommentController : ControllerBase
{
    private readonly IRatingService _ratingService;
    private readonly ILogger<CommentController> _logger;

    public CommentController(IRatingService ratingService, ILogger<CommentController> logger)
    {
        _ratingService = ratingService;
        _logger = logger;
    }

    [HttpPost("{commentId}/vote")]
    [Authorize]
    public async Task<IActionResult> VoteForComment(int commentId, [FromBody] VoteRequest request)
    {
        try
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdStr == null) return Unauthorized();
            var userId = int.Parse(userIdStr);

            // Validate vote
            if (request.Vote != -1 && request.Vote != 1)
            {
                return BadRequest(new ErrorResponse { Error = "Vote must be -1 or 1" });
            }

            var response = await _ratingService.VoteForCommentAsync(userId, commentId, request.Vote);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ErrorResponse { Error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ErrorResponse { Error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error voting for comment {CommentId}", commentId);
            return StatusCode(500, new ErrorResponse { Error = "Internal server error" });
        }
    }
}