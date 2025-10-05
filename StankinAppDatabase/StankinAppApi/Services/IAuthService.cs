using System.Security.Claims;
using StankinAppApi.Models;

namespace StankinAppApi.Services;

public interface IAuthService
{
    Task<(string Jwt, User User)> AuthenticateYandexUserAsync(string code);
    Task<(string Jwt, User User)> AuthenticateWithYandexTokenAsync(string accessToken); // Added
    string GenerateJwtToken(int userId, long yandexId);
    ClaimsPrincipal ValidateToken(string token);
}