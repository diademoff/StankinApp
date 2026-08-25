using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace StankinAppApi.Board;

public static class BoardGuard
{
    // ponytail: in-memory rate-limit жалоб, сбрасывается при рестарте — для 1/мин достаточно
    private static readonly ConcurrentDictionary<string, DateTime> _lastReports = new();

    public static string GetClientIp(HttpContext ctx)
    {
        var fwd = ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(fwd))
            return fwd.Split(',')[0].Trim();
        return ctx.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "";
    }

    public static string HashIp(string ip) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ip)));

    public static IResult CheckPostAllowed(HttpContext ctx, BoardRepository repo, GeoIpService geo)
    {
        var ip = GetClientIp(ctx);
        if (repo.IsBanned(HashIp(ip)))
            return Results.Json(new { error = "Доступ запрещён" }, statusCode: 403);
        if (!geo.IsRussia(ip))
            return Results.Json(new { error = "Доска доступна только с российских IP" }, statusCode: 403);
        return null;
    }

    public static bool IsReportRateLimited(string ip)
    {
        var now = DateTime.UtcNow;
        if (_lastReports.TryGetValue(ip, out var last) && now - last < TimeSpan.FromMinutes(1))
            return true;
        _lastReports[ip] = now;
        return false;
    }
}
