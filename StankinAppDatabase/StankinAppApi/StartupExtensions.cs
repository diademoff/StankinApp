using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Net.Http.Headers;
using Serilog;
using Serilog.Events;
using StankinAppApi.Board;
using StankinAppApi.Dto;
using StankinAppCore;

namespace StankinAppApi;

static class StartupExtensions
{
    static bool IsScheduleEndpoint(PathString path) =>
        path.StartsWithSegments("/api/schedule") ||
        path.StartsWithSegments("/api/groups") ||
        path.StartsWithSegments("/api/teachers") ||
        path.StartsWithSegments("/api/rooms");

    static bool ScheduleDbReady(string dbPath)
    {
        if (!File.Exists(dbPath)) return false;
        try
        {
            using var conn = new SqliteConnection($"Data Source={dbPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT count(*) FROM sqlite_master
                WHERE type = 'table'
                  AND name IN ('lessons','sessions','groups','teachers','rooms','schedule_dates')
                """;
            return Convert.ToInt64(cmd.ExecuteScalar()) == 6;
        }
        catch (SqliteException)
        {
            return false;
        }
    }

    const int MaxPostLength = 4000;

    static string[] AvailableIp =
    [
      "https://stankinapp.ru"
    ];

    public static void ConfigureLogging(this WebApplicationBuilder builder)
    {
        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .MinimumLevel.Error()
            // лог нагрузки (10-сек окно) пишется на Information в своём namespace
            .MinimumLevel.Override("StankinAppApi.Diagnostics", LogEventLevel.Information)
            .WriteTo.Console()
            .CreateLogger();

        builder.Host.UseSerilog();
    }

    public static void ConfigureKestrel(this WebApplicationBuilder builder)
    {
        builder.WebHost.ConfigureKestrel(opts =>
        {
            opts.Limits.MaxRequestBodySize = 10 * 1024 * 1024;
            opts.AddServerHeader = false;
        });
    }

    public static void ConfigureCors(this WebApplicationBuilder builder)
    {
#if DEBUG
        builder.Services.AddCors(o => o.AddPolicy("AllowFrontend", p =>
            p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()
        ));
#else
        builder.Services.AddCors(o => o.AddPolicy("AllowFrontend", p =>
            p.WithOrigins(AvailableIp).AllowAnyHeader().AllowAnyMethod()
        ));
#endif
    }

    public static void ConfigureServices(this WebApplicationBuilder builder)
    {
        var configuration = builder.Configuration;

        builder.Services.AddControllers()
            .AddJsonOptions(opts =>
            {
                opts.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                opts.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
                opts.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
            });

        builder.Services.AddMemoryCache();

        var dbPath = configuration.GetValue<string>("Database:Path");
        if (string.IsNullOrEmpty(dbPath))
            throw new InvalidOperationException("Database path is not configured in appsettings.");

        var absoluteDbPath = Path.IsPathRooted(dbPath)
            ? dbPath
            : Path.Combine(builder.Environment.ContentRootPath, dbPath);

        var debugMode = configuration.GetValue<bool>("Debug:Enabled");
        if (debugMode)
        {
            Console.Error.WriteLine("[Debug] Using in-memory mock data");
            builder.Services.AddSingleton<IDataReader>(_ => new StankinAppCore.MockDataReader());
        }
        else
        {
            builder.Services.AddSingleton<IDataReader>(_ => new DatabaseReader(absoluteDbPath));
        }
        builder.Services.AddSingleton<ScheduleService>();

        var boardDbPath = configuration.GetValue<string>("Board:DbPath") ?? "data/board.db";
        var absoluteBoardDbPath = Path.IsPathRooted(boardDbPath)
            ? boardDbPath
            : Path.Combine(builder.Environment.ContentRootPath, boardDbPath);
        var pinnedThreadIds = configuration.GetSection("Board:PinnedThreads").Get<List<long>>() ?? new List<long>();
        var boardRepo = new BoardRepository(absoluteBoardDbPath, pinnedThreadIds);
        boardRepo.EnsureSchema();
        builder.Services.AddSingleton(boardRepo);

        var geoDbPath = configuration.GetValue<string>("GeoIp:DbPath") ?? "data/GeoLite2-Country.mmdb";
        var absoluteGeoDbPath = Path.IsPathRooted(geoDbPath)
            ? geoDbPath
            : Path.Combine(builder.Environment.ContentRootPath, geoDbPath);
        builder.Services.AddSingleton(new GeoIpService(absoluteGeoDbPath));
        builder.Services.AddSingleton<CaptchaService>();
        builder.Services.AddHttpClient();
    }

    public static void MapApi(this WebApplication app)
    {
        // в debug расписание из mock-данных, гейт нужен только для реальной БД
        var debugMode = app.Configuration.GetValue<bool>("Debug:Enabled");
        var dbPath = app.Configuration.GetValue<string>("Database:Path");
        var absoluteDbPath = Path.IsPathRooted(dbPath) ? dbPath : Path.Combine(app.Environment.ContentRootPath, dbPath);

        // готовность БД проверяем до первого успеха; дальше — без обращения к файлу на каждый запрос
        var gate = new DbReadinessGate(debugMode ? null : absoluteDbPath);
        app.Use(async (ctx, next) =>
        {
            if (!gate.IsReady() && IsScheduleEndpoint(ctx.Request.Path))
            {
                ctx.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                await ctx.Response.WriteAsJsonAsync(new { error = "Расписание скоро появится" });
                return;
            }
            await next();
        });

        app.MapControllers();

        app.MapGet("/api/groups", (HttpContext ctx, IMemoryCache cache, ScheduleService service, ILogger<Program> log) =>
        {
            log.LogInformation("GET /api/groups");
            return CachedList(ctx, cache, "list:groups", service.GetGroups, log);
        });

        app.MapGet("/api/rooms", (HttpContext ctx, IMemoryCache cache, ScheduleService service, ILogger<Program> log) =>
        {
            log.LogInformation("GET /api/rooms");
            return CachedList(ctx, cache, "list:rooms", service.GetRooms, log);
        });

        app.MapGet("/api/teachers", (HttpContext ctx, IMemoryCache cache, ScheduleService service, ILogger<Program> log) =>
        {
            log.LogInformation("GET /api/teachers");
            return CachedList(ctx, cache, "list:teachers", service.GetTeachers, log);
        });

        app.MapGet("/api/teachers/validate", (string name, ScheduleService service, ILogger<Program> log) =>
        {
            if (string.IsNullOrWhiteSpace(name))
                return Results.BadRequest(new { error = "Missing 'name' parameter" });

            var exists = service.GetTeachers().Contains(name, StringComparer.OrdinalIgnoreCase);
            log.LogInformation("Validated teacher '{TeacherName}': {Exists}", name, exists);
            return Results.Ok(new { exists });
        });

        app.MapGet("/api/schedule",
            (string groupName, string startDate, string endDate,
             ScheduleService service, IMemoryCache cache, ILogger<Program> log) =>
        {
            if (string.IsNullOrWhiteSpace(groupName) ||
                string.IsNullOrWhiteSpace(startDate)  ||
                string.IsNullOrWhiteSpace(endDate))
            {
                log.LogWarning("Missing parameters: group={Group}, start={Start}, end={End}",
                    groupName, startDate, endDate);
                return Results.BadRequest(new { error = "groupName, startDate и endDate обязательны" });
            }

            if (!DateOnly.TryParseExact(startDate, "yyyy-MM-dd", out var parsedStart) ||
                !DateOnly.TryParseExact(endDate,   "yyyy-MM-dd", out var parsedEnd))
            {
                return Results.BadRequest(new { error = "Даты должны быть в формате yyyy-MM-dd" });
            }

            if (parsedEnd < parsedStart)
                return Results.BadRequest(new { error = "endDate не может быть раньше startDate" });

            var cacheKey = $"sched:{groupName}:{startDate}:{endDate}";
            List<CourseDto> lessons;

            if (!cache.TryGetValue(cacheKey, out lessons))
            {
                try
                {
                    lessons = service.GetMergedScheduleForGroup(groupName, startDate, endDate).ToList();
                    cache.Set(cacheKey, lessons, TimeSpan.FromHours(2));
                    log.LogInformation("Fetched from DB & cached: {Key}", cacheKey);
                }
                catch (Exception ex)
                {
                    log.LogError(ex, "Error fetching schedule for {Group}", groupName);
                    return Results.Json(new { error = "Внутренняя ошибка сервера" }, statusCode: 500);
                }
            }
            else
            {
                log.LogInformation("Served from cache: {Key}", cacheKey);
            }

            if (lessons.Count == 0)
                return Results.NoContent();

            return Results.Ok(new ListResponse<CourseDto>(lessons));
        });


       app.MapGet("/api/schedule/teacher", (
            string teacherName,
            string startDate,
            string endDate,
            ScheduleService service,
            IMemoryCache cache,
            ILogger<Program> log) =>
        {
            if (string.IsNullOrWhiteSpace(teacherName) ||
                string.IsNullOrWhiteSpace(startDate)   ||
                string.IsNullOrWhiteSpace(endDate))
            {
                log.LogWarning("Missing parameters: teacher={Teacher}, start={Start}, end={End}",
                    teacherName, startDate, endDate);
                return Results.BadRequest(new { error = "teacherName, startDate и endDate обязательны" });
            }

            if (!DateOnly.TryParseExact(startDate, "yyyy-MM-dd", out var parsedStart) ||
                !DateOnly.TryParseExact(endDate,   "yyyy-MM-dd", out var parsedEnd))
            {
                return Results.BadRequest(new { error = "Даты должны быть в формате yyyy-MM-dd" });
            }

            if (parsedEnd < parsedStart)
                return Results.BadRequest(new { error = "endDate не может быть раньше startDate" });

            var cacheKey = $"sched:teacher:{teacherName}:{startDate}:{endDate}";
            List<CourseDto> lessons;

            if (!cache.TryGetValue(cacheKey, out lessons))
            {
                try
                {
                    lessons = service.GetMergedScheduleForTeacher(teacherName, startDate, endDate).ToList();
                    cache.Set(cacheKey, lessons, TimeSpan.FromHours(2));
                    log.LogInformation("Fetched from DB & cached: {Key}", cacheKey);
                }
                catch (Exception ex)
                {
                    log.LogError(ex, "Error fetching schedule for {Teacher}", teacherName);
                    return Results.Json(new { error = "Внутренняя ошибка сервера" }, statusCode: 500);
                }
            }
            else
            {
                log.LogInformation("Served from cache: {Key}", cacheKey);
            }

            if (lessons.Count == 0)
                return Results.NoContent();

            return Results.Ok(new ListResponse<CourseDto>(lessons));
        });

        app.MapGet("/api/schedule/by-subject", (
            string subject,
            string teacher,
            string groupName,
            ScheduleService service,
            IMemoryCache cache,
            ILogger<Program> log) =>
        {
            if (string.IsNullOrWhiteSpace(subject) ||
                string.IsNullOrWhiteSpace(teacher) ||
                string.IsNullOrWhiteSpace(groupName))
            {
                log.LogWarning("Missing parameters: subject={Subject}, teacher={Teacher}, groupName={GroupName}",
                    subject, teacher, groupName);
                return Results.BadRequest(new { error = "subject, teacher и groupName обязательны" });
            }

            var today = DateTime.Today;
            var startDate = today.AddMonths(-6).ToString("yyyy-MM-dd");
            var endDate   = today.AddMonths(6).ToString("yyyy-MM-dd");

            var cacheKey = $"sched:subject:{subject}:{teacher}:{groupName}";
            List<CourseDto> lessons;

            if (!cache.TryGetValue(cacheKey, out lessons))
            {
                try
                {
                    lessons = service.GetScheduleBySubject(subject, teacher, groupName, startDate, endDate).ToList();
                    cache.Set(cacheKey, lessons, TimeSpan.FromHours(2));
                    log.LogInformation("Fetched from DB & cached: {Key}", cacheKey);
                }
                catch (Exception ex)
                {
                    log.LogError(ex, "Error fetching schedule by subject for {Subject}, {Teacher}, {GroupName}",
                        subject, teacher, groupName);
                    return Results.Json(new { error = "Внутренняя ошибка сервера" }, statusCode: 500);
                }
            }
            else
            {
                log.LogInformation("Served from cache: {Key}", cacheKey);
            }

            if (lessons.Count == 0)
                return Results.NoContent();

            return Results.Ok(new ListResponse<CourseDto>(lessons));
        });
    }

    public static void MapBoardApi(this WebApplication app)
    {
        var bumpLimit = app.Configuration.GetValue<int>("Board:BumpLimit", 50);
        var pageSize = app.Configuration.GetValue<int>("Board:PageSize", 20);
        var cacheSeconds = app.Configuration.GetValue<int>("Board:CacheSeconds", 10);
        var cache = app.Services.GetRequiredService<IMemoryCache>();

        // чтения кэшируются на cacheSeconds; ключ содержит Revision репозитория,
        // поэтому после записи список/тред отдаются сразу актуальными
        app.Use(async (ctx, next) =>
        {
            if (ctx.Request.Path.StartsWithSegments("/api/admin"))
            {
                var secret = app.Configuration.GetValue<string>("Moderator:Secret");
                var provided = ctx.Request.Headers["X-Admin-Secret"].FirstOrDefault();
                if (string.IsNullOrEmpty(secret) || !string.Equals(provided, secret, StringComparison.Ordinal))
                {
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await ctx.Response.WriteAsJsonAsync(new { error = "Unauthorized" });
                    return;
                }
            }
            await next();
        });

        app.MapGet("/api/board/threads", (int? page, BoardRepository repo) =>
        {
            var p = Math.Max(1, page ?? 1);
            var key = $"board:list:{repo.Revision}:{p}";
            var items = cache.GetOrCreate(key, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(cacheSeconds);
                return repo.GetThreads(p, pageSize).Select(t =>
                    new ThreadSummaryDto(t.Op.Id, BoardMapper.ToDto(t.Op), t.ReplyCount, t.Op.UpdatedAt,
                        t.LastReplies.Select(BoardMapper.ToDto).ToList(), t.IsPinned)).ToList();
            })!;
            return Results.Ok(new ListResponse<ThreadSummaryDto>(items));
        });

        app.MapGet("/api/board/stats", (string since, BoardRepository repo) =>
        {
            DateTime? sinceDate = null;
            if (!string.IsNullOrWhiteSpace(since)
                && DateTime.TryParse(since, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
                sinceDate = parsed;
            return Results.Ok(new { newThreads = repo.CountNewThreads(sinceDate) });
        });

        app.MapGet("/api/board/threads/{threadId:long}", (long threadId, BoardRepository repo) =>
        {
            var key = $"board:thread:{repo.Revision}:{threadId}";
            var dto = cache.GetOrCreate(key, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(cacheSeconds);
                var posts = repo.GetThread(threadId);
                return posts.Count == 0
                    ? null
                    : new ThreadDetailDto(threadId, posts.Select(BoardMapper.ToDto).ToList());
            });
            if (dto == null)
                return Results.NotFound();
            return Results.Ok(dto);
        });

        app.MapPost("/api/board/threads",
            async (BoardRequest req, HttpContext ctx, BoardRepository repo, GeoIpService geo, CaptchaService captcha) =>
        {
            var forbidden = BoardGuard.CheckPostAllowed(ctx, repo, geo);
            if (forbidden != null) return forbidden;
            if (req == null || string.IsNullOrWhiteSpace(req.Text))
                return Results.BadRequest(new { error = "Пустое сообщение" });
            if (req.Text.Length > MaxPostLength)
                return Results.BadRequest(new { error = $"Сообщение не длиннее {MaxPostLength} символов" });

            var ip = BoardGuard.GetClientIp(ctx);
            var (ok, err) = await captcha.ValidateAsync(req.CaptchaToken, ip);
            if (!ok) return Results.BadRequest(new { error = err });

            var post = repo.CreateThread(req.Text.Trim(), BoardGuard.HashIp(ip));
            return Results.Created($"/api/board/threads/{post.Id}", BoardMapper.ToDto(post));
        });

        app.MapPost("/api/board/threads/{threadId:long}/posts",
            async (long threadId, BoardRequest req, HttpContext ctx, BoardRepository repo, GeoIpService geo, CaptchaService captcha) =>
        {
            var forbidden = BoardGuard.CheckPostAllowed(ctx, repo, geo);
            if (forbidden != null) return forbidden;
            if (req == null || string.IsNullOrWhiteSpace(req.Text))
                return Results.BadRequest(new { error = "Пустое сообщение" });
            if (req.Text.Length > MaxPostLength)
                return Results.BadRequest(new { error = $"Сообщение не длиннее {MaxPostLength} символов" });

            var ip = BoardGuard.GetClientIp(ctx);
            var (ok, err) = await captcha.ValidateAsync(req.CaptchaToken, ip);
            if (!ok) return Results.BadRequest(new { error = err });

            try
            {
                var (post, _) = repo.AddReply(threadId, req.ParentId, req.Text.Trim(), BoardGuard.HashIp(ip), req.Sage, bumpLimit);
                return Results.Created($"/api/board/threads/{threadId}#post-{post.Id}", BoardMapper.ToDto(post));
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { error = "Тред не найден" });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapPost("/api/board/posts/{postId:long}/report", (long postId, HttpContext ctx, BoardRepository repo) =>
        {
            if (BoardGuard.IsReportRateLimited(BoardGuard.GetClientIp(ctx)))
                return Results.Json(new { error = "Слишком частые жалобы" }, statusCode: 429);
            return repo.Report(postId) ? Results.NoContent() : Results.NotFound();
        });

        app.MapGet("/api/admin/reports", (BoardRepository repo) =>
        {
            var reports = repo.GetReports();
            return Results.Ok(new ListResponse<ReportDto>(reports.Select(p =>
                new ReportDto(p.Id, p.ThreadId, p.Text, p.ReportCount, p.IpHash, p.CreatedAt))));
        });

        app.MapDelete("/api/admin/posts/{postId:long}", (long postId, BoardRepository repo) =>
            repo.SoftDelete(postId) ? Results.NoContent() : Results.NotFound());

        app.MapPost("/api/admin/reports/{postId:long}/dismiss", (long postId, BoardRepository repo) =>
            repo.DismissReports(postId) ? Results.NoContent() : Results.NotFound());

        app.MapPost("/api/admin/ban", (BanRequest req, BoardRepository repo) =>
        {
            if (string.IsNullOrWhiteSpace(req?.IpHash))
                return Results.BadRequest(new { error = "ipHash обязателен" });
            repo.Ban(req.IpHash.Trim());
            return Results.NoContent();
        });
    }

    // Готовность БД кэшируем после первого успеха: повторная проверка через SQLite
    // на каждый запрос не нужна, пока файл БД не заменили (ребилд идёт редко).
    sealed class DbReadinessGate
    {
        private readonly string _dbPath;
        private int _ready;

        public DbReadinessGate(string dbPath) => _dbPath = dbPath;

        public bool IsReady()
        {
            if (_dbPath == null) return true;
            if (Volatile.Read(ref _ready) == 1) return true;
            if (!ScheduleDbReady(_dbPath)) return false;
            Volatile.Write(ref _ready, 1);
            return true;
        }
    }

    sealed record CachedListValue(string[] Items, string Etag);

    // Справочные списки: кэш в памяти 12ч + Cache-Control/ETag/304,
    // чтобы SW-ревалидация и повторы браузера не дёргали SQLite.
    static Task<IResult> CachedList(HttpContext ctx, IMemoryCache cache, string key,
        Func<IEnumerable<string>> load, ILogger<Program> log)
    {
        var ttl = TimeSpan.FromHours(12);
        if (!cache.TryGetValue(key, out CachedListValue entry))
        {
            var items = load().ToArray();
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\u001F', items)));
            entry = new CachedListValue(items, $"\"{Convert.ToHexString(hash)}\"");
            cache.Set(key, entry, ttl);
            log.LogInformation("List {Key} fetched from DB & cached", key);
        }
        else
        {
            log.LogInformation("List {Key} served from cache", key);
        }

        ctx.Response.GetTypedHeaders().CacheControl = new CacheControlHeaderValue { Public = true, MaxAge = ttl };
        ctx.Response.GetTypedHeaders().ETag = new EntityTagHeaderValue(entry.Etag);

        foreach (var candidate in ctx.Request.Headers.IfNoneMatch)
        {
            if (candidate == "*" || string.Equals(candidate?.Trim(), entry.Etag, StringComparison.Ordinal))
                return Task.FromResult(Results.StatusCode(StatusCodes.Status304NotModified));
        }

        return Task.FromResult<IResult>(Results.Ok(new ListResponse<string>(entry.Items)));
    }
}
