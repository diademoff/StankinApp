using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace StankinAppApi.Diagnostics;

public sealed class RequestRateTracker
{
    private long _count;
    private long _sumMs;
    private long _maxMs;
    private ConcurrentDictionary<string, long> _byPrefix = new();

    public void Record(string path, double elapsedMs)
    {
        Interlocked.Increment(ref _count);
        var ms = (long)elapsedMs;
        Interlocked.Add(ref _sumMs, ms);
        long cur;
        while (ms > (cur = Volatile.Read(ref _maxMs)))
            Interlocked.CompareExchange(ref _maxMs, ms, cur);
        var prefix = Bucket(path);
        _byPrefix.AddOrUpdate(prefix, 1, (_, n) => n + 1);
    }

    // атомарно забираем окно и начинаем новое; расхождение на границе окна несущественно
    public (long Count, long SumMs, long MaxMs, KeyValuePair<string, long>[] Buckets) SwapWindow()
    {
        var count = Interlocked.Exchange(ref _count, 0);
        var sum = Interlocked.Exchange(ref _sumMs, 0);
        var max = Interlocked.Exchange(ref _maxMs, 0);
        var old = Interlocked.Exchange(ref _byPrefix, new ConcurrentDictionary<string, long>());
        return (count, sum, max, old.ToArray());
    }

    private static string Bucket(string path)
    {
        if (!path.StartsWith("/api/", StringComparison.Ordinal)) return "other";
        var seg = path.Split('/');
        var area = seg.Length > 2 ? seg[2] : "";
        switch (area)
        {
            case "schedule": return "/api/schedule";
            case "groups":   return "/api/groups";
            case "teachers": return "/api/teachers";
            case "rooms":    return "/api/rooms";
            case "board":
                var sub = seg.Length > 3 ? seg[3] : "";
                if (sub == "threads")
                    return seg.Length > 4 && long.TryParse(seg[4], out _) ? "/api/board/threads/{id}" : "/api/board/threads";
                if (sub == "posts") return "/api/board/posts";
                if (sub == "stats") return "/api/board/stats";
                return "/api/board";
            case "admin": return "/api/admin";
            default: return area.Length == 0 ? "/api" : $"/api/{area}";
        }
    }
}

public static class RequestRateLogging
{
    public static IServiceCollection AddRequestRateLogging(this IServiceCollection services)
    {
        services.AddSingleton<RequestRateTracker>();
        services.AddHostedService<RequestRateLoggerService>();
        return services;
    }

    public static IApplicationBuilder UseRequestRateLogging(this IApplicationBuilder app)
    {
        var tracker = app.ApplicationServices.GetRequiredService<RequestRateTracker>();
        app.Use(async (ctx, next) =>
        {
            var sw = Stopwatch.StartNew();
            try
            {
                await next();
            }
            finally
            {
                sw.Stop();
                tracker.Record(ctx.Request.Path.Value ?? "", sw.Elapsed.TotalMilliseconds);
            }
        });
        return app;
    }
}

public sealed class RequestRateLoggerService : BackgroundService
{
    private readonly RequestRateTracker _tracker;
    private readonly ILogger<RequestRateLoggerService> _log;
    private readonly TimeSpan _window;

    public RequestRateLoggerService(RequestRateTracker tracker, IConfiguration config, ILogger<RequestRateLoggerService> log)
    {
        _tracker = tracker;
        _log = log;
        var seconds = config.GetValue<int>("Diagnostics:RateWindowSeconds", 10);
        _window = TimeSpan.FromSeconds(Math.Max(1, seconds));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_window);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var (count, sumMs, maxMs, buckets) = _tracker.SwapWindow();
            var rate = count == 0 ? 0 : count / _window.TotalSeconds;
            var avgMs = count == 0 ? 0 : sumMs / count;

            var sb = new StringBuilder($"Requests {count} in last {_window.TotalSeconds:0}s ({rate:F1}/s) avg={avgMs}ms max={maxMs}ms");
            foreach (var b in buckets.OrderByDescending(kv => kv.Value).Take(5))
                sb.Append($" | {b.Key}: {b.Value}");
            _log.LogInformation("{Metric}", sb.ToString());
        }
    }
}
