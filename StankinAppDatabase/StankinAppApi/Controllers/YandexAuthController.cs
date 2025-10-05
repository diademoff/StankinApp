// StankinAppDatabase/StankinAppApi/Controllers/YandexAuthController.cs

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StankinAppApi.Dto;
using StankinAppApi.Services;
using System;
using System.Threading.Tasks;

namespace StankinAppApi.Controllers;

[ApiController]
[Route("api/auth/yandex")]
public class YandexAuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<YandexAuthController> _logger;
    private readonly string _yandexClientId;
    private readonly string _frontendUrl;

    public YandexAuthController(IAuthService authService, ILogger<YandexAuthController> logger, IConfiguration configuration)
    {
        _authService = authService;
        _logger = logger;
        _yandexClientId = configuration["Yandex:ClientId"] ?? throw new InvalidOperationException("Yandex:ClientId not configured");
        // URL фронтенда, куда будет редирект после успешной авторизации
        _frontendUrl = configuration["FrontendUrl"] ?? "https://stankinapp.ru";
    }

    [HttpGet("login")]
    public IActionResult Login([FromQuery] string from = "/")
    {
        // `from` - это путь, куда вернуть пользователя после логина
        var state = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(from));
        var redirectUrl = $"https://oauth.yandex.ru/authorize?response_type=code&client_id={_yandexClientId}&state={state}";

        _logger.LogInformation("Redirecting user to Yandex for authentication.");
        return Redirect(redirectUrl);
    }

    [HttpGet("callback")]
    public async Task<IActionResult> YandexCallback([FromQuery] string code, [FromQuery] string state)
    {
        try
        {
            if (string.IsNullOrEmpty(code))
            {
                _logger.LogWarning("Yandex callback called without an authorization code.");
                return BadRequest(new ErrorResponse { Error = "Authorization code is missing." });
            }

            _logger.LogInformation("Received callback from Yandex with an authorization code.");
            var (jwt, user) = await _authService.AuthenticateYandexUserAsync(code);

            // Редирект обратно на фронтенд с токеном в URL-фрагменте
            // Фронтенд должен будет прочитать этот токен и сохранить его
            var decodedState = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(state));
            var redirectUrl = $"{_frontendUrl}{decodedState}#jwt={jwt}";

            _logger.LogInformation("Successfully authenticated user {UserId} ({Username}). Redirecting to frontend.", user.Id, user.Username);
            return Redirect(redirectUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred during Yandex callback processing.");
            var errorRedirectUrl = $"{_frontendUrl}/auth-error";
            return Redirect(errorRedirectUrl);
        }
    }
}