using Microsoft.AspNetCore.Mvc;
using StankinAppApi.Services;
using StankinAppApi.Dto;
using Serilog;

namespace StankinAppApi.Controllers;

[ApiController]
[Route("api/auth/yandex")]
public class YandexAuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<YandexAuthController> _logger;

    public YandexAuthController(IAuthService authService, ILogger<YandexAuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    [HttpGet("login")]
    public IActionResult Login([FromQuery] string from = null)
    {
        var clientId = HttpContext.RequestServices.GetRequiredService<IConfiguration>()["Yandex:ClientId"];
        var redirectUri = $"{Request.Scheme}://{Request.Host}/api/auth/yandex/callback";
        if (!string.IsNullOrEmpty(from))
        {
            redirectUri += $"?from={Uri.EscapeDataString(from)}";
        }

        var authUrl = $"https://oauth.yandex.ru/authorize?response_type=code&client_id={clientId}&redirect_uri={Uri.EscapeDataString(redirectUri)}";

        _logger.LogInformation("Redirecting to Yandex OAuth login");
        return Redirect(authUrl);
    }

    [HttpGet("callback")]
    public async Task<IActionResult> Callback(string code, [FromQuery] string from = null)
    {
        if (string.IsNullOrEmpty(code))
        {
            _logger.LogWarning("Missing code in Yandex callback");
            return BadRequest(new ErrorResponse { Error = "Missing authorization code" });
        }

        try
        {
            var (jwt, user) = await _authService.AuthenticateYandexUserAsync(code);
            _logger.LogInformation("Yandex authentication successful for user {UserId}", user.Id);

            var redirectUrl = from ?? "/";
            var uriBuilder = new UriBuilder(redirectUrl);
            uriBuilder.Fragment = $"access_token={jwt}";

            return Redirect(uriBuilder.Uri.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Yandex callback");
            return StatusCode(500, new ErrorResponse { Error = "Authentication failed" });
        }
    }

    [HttpPost("token")]
    public async Task<IActionResult> ExchangeToken([FromBody] YandexTokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.AccessToken))
        {
            _logger.LogWarning("missing access_token in exchange request");
            return BadRequest(new ErrorResponse { Error = "missing access_token" });
        }

        try
        {
            var (jwt, user) = await _authService.AuthenticateWithYandexTokenAsync(request.AccessToken);

            _logger.LogInformation("yandex token exchange successful for user {UserId}", user.Id);

            var response = new AuthResponse
            {
                Token = jwt,
                User = new UserDto
                {
                    Id = user.Id,
                    FirstName = user.FirstName,
                    Username = user.Username,
                    PhotoUrl = user.PhotoUrl
                }
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "error exchanging yandex token");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new ErrorResponse { Error = "token exchange failed" });
        }
    }
}
public class YandexTokenRequest
{
    public string AccessToken { get; set; }
}