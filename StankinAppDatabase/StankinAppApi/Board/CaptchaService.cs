using System.Net;
using System.Text.Json;

namespace StankinAppApi.Board;

public class CaptchaService
{
    private const string ValidateUrl = "https://smartcaptcha.cloud.yandex.ru/validate";
    private const int MonthlyQuota = 6000;

    private readonly string _secret;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<CaptchaService> _log;
    private readonly BoardRepository _repo;

    public CaptchaService(IConfiguration config, IHttpClientFactory http, ILogger<CaptchaService> log, BoardRepository repo)
    {
        _secret = config.GetValue<string>("Captcha:ServerSecret") ?? "";
        _http = http;
        _log = log;
        _repo = repo;
    }

    public async Task<(bool Ok, string Error)> ValidateAsync(string token, string ip)
    {
        if (string.IsNullOrEmpty(_secret))
            return (true, null); // ponytail: dev без серверного ключа → пропускаем

        if (string.IsNullOrWhiteSpace(token))
            return (false, "Капча не пройдена");

        using var client = _http.CreateClient();
        var form = new Dictionary<string, string> { ["secret"] = _secret, ["token"] = token, ["ip"] = ip };
        var resp = await client.PostAsync(ValidateUrl, new FormUrlEncodedContent(form));

        var usage = _repo.IncrementCaptcha(DateTime.UtcNow.ToString("yyyy-MM"));
        _log.LogWarning("Расход капчи за текущий месяц: {Usage}/{Quota}", usage, MonthlyQuota);

        if (resp.StatusCode != HttpStatusCode.OK)
        {
            _log.LogWarning("Капча ответила кодом {Code}, пропускаем (fail-open по докам Яндекса)", resp.StatusCode);
            return (true, null);
        }

        var body = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var ok = body.RootElement.GetProperty("status").GetString() == "ok";
        return (ok, ok ? null : "Капча не пройдена");
    }
}
