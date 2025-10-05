using Microsoft.IdentityModel.Tokens;
using StankinAppApi.Dto;
using StankinAppApi.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace StankinAppApi.Services;

public class AuthService : IAuthService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;
    private readonly IRatingService _ratingService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _jwtSecret;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly string _yandexClientId;
    private readonly string _yandexClientSecret;

    public AuthService(IConfiguration configuration, ILogger<AuthService> logger, IRatingService ratingService, IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _logger = logger;
        _ratingService = ratingService;
        _httpClientFactory = httpClientFactory;

        _jwtSecret = configuration["Jwt:Secret"] ?? throw new InvalidOperationException("Jwt:Secret not configured");
        _issuer = configuration["Jwt:Issuer"] ?? "StankinApp";
        _audience = configuration["Jwt:Audience"] ?? "StankinApp";
        _yandexClientId = configuration["Yandex:ClientId"] ?? throw new InvalidOperationException("Yandex:ClientId not configured");
        _yandexClientSecret = configuration["Yandex:ClientSecret"] ?? throw new InvalidOperationException("Yandex:ClientSecret not configured");
    }

    // Для code flow
    public async Task<(string Jwt, User User)> AuthenticateYandexUserAsync(string code)
    {
        try
        {
            // 1. Обменять authorization_code на access_token
            var token = await GetYandexTokenAsync(code);
            if (token == null || string.IsNullOrEmpty(token.AccessToken))
            {
                throw new InvalidOperationException("Failed to get Yandex access token.");
            }

            // 2. Получить информацию о пользователе
            var userInfo = await GetYandexUserInfoAsync(token.AccessToken);
            if (userInfo == null || !long.TryParse(userInfo.Id, out var yandexId))
            {
                throw new InvalidOperationException("Failed to get Yandex user info.");
            }

            // 3. Найти или создать пользователя в нашей БД
            var user = await _ratingService.GetOrCreateUserAsync(
                yandexId,
                userInfo.Login,
                userInfo.FirstName ?? userInfo.RealName,
                userInfo.DefaultEmail,
                !userInfo.IsAvatarEmpty ? $"https://avatars.yandex.net/get-yapic/{userInfo.DefaultAvatarId}/islands-200" : null
            );

            // 4. Сгенерировать наш JWT
            var jwt = GenerateJwtToken(user.Id, user.YandexId);

            _logger.LogInformation("Yandex code flow authentication successful for user {UserId}", user.Id);
            return (jwt, user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Yandex code flow authentication process.");
            throw;
        }
    }

    // Новый метод для implicit flow: напрямую с access_token
    public async Task<(string Jwt, User User)> AuthenticateWithYandexTokenAsync(string accessToken)
    {
        try
        {
            // Получить информацию о пользователе напрямую с access_token
            var userInfo = await GetYandexUserInfoAsync(accessToken);
            if (userInfo == null || !long.TryParse(userInfo.Id, out var yandexId))
            {
                throw new InvalidOperationException("Failed to get Yandex user info.");
            }

            // Найти или создать пользователя
            var user = await _ratingService.GetOrCreateUserAsync(
                yandexId,
                userInfo.Login,
                userInfo.FirstName ?? userInfo.RealName,
                userInfo.DefaultEmail,
                !userInfo.IsAvatarEmpty ? $"https://avatars.yandex.net/get-yapic/{userInfo.DefaultAvatarId}/islands-200" : null
            );

            // Сгенерировать JWT
            var jwt = GenerateJwtToken(user.Id, user.YandexId);

            _logger.LogInformation("Yandex implicit flow authentication successful for user {UserId}", user.Id);
            return (jwt, user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Yandex implicit flow authentication process.");
            throw;
        }
    }

    private async Task<YandexTokenResponse> GetYandexTokenAsync(string code)
    {
        var client = _httpClientFactory.CreateClient();
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            {"grant_type", "authorization_code"},
            {"code", code},
            {"client_id", _yandexClientId},
            {"client_secret", _yandexClientSecret}
        });

        var response = await client.PostAsync("https://oauth.yandex.ru/token", content);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("Yandex token request failed: {StatusCode} - {Error}", response.StatusCode, error);
            return null;
        }

        var tokenResponse = await response.Content.ReadFromJsonAsync<YandexTokenResponse>();
        if (tokenResponse != null)
        {
            _logger.LogInformation("Yandex token obtained successfully");
        }
        return tokenResponse;
    }

    private async Task<YandexUserResponse> GetYandexUserInfoAsync(string accessToken)
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("OAuth", accessToken);

        var response = await client.GetAsync("https://login.yandex.ru/info?format=json");
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("Yandex user info request failed: {StatusCode} - {Error}", response.StatusCode, error);
            return null;
        }

        var userResponse = await response.Content.ReadFromJsonAsync<YandexUserResponse>();
        if (userResponse != null)
        {
            _logger.LogInformation("Yandex user info obtained successfully for user {YandexId}", userResponse.Id);
        }
        return userResponse;
    }

    public string GenerateJwtToken(int userId, long yandexId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("yandex_id", yandexId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat,
                new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64)
        };
        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddDays(30),
            signingCredentials: credentials
        );
        var jwt = new JwtSecurityTokenHandler().WriteToken(token);
        _logger.LogInformation("Generated JWT for user {UserId}", userId);
        return jwt;
    }

    public ClaimsPrincipal ValidateToken(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_jwtSecret);

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _issuer,
                ValidateAudience = true,
                ValidAudience = _audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };
            var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);
            _logger.LogInformation("Token validated successfully");
            return principal;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token validation failed");
            return null;
        }
    }
}