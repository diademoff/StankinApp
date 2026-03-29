using System.Text.Json;
using System.Text.Json.Serialization;

namespace StankinAppApi;

static class StartupExtensions
{
    static string[] AvailableIp =
    [
      "stankinapp.ru"
    ];
    public static void ConfigureLogging(this WebApplicationBuilder builder)
    {
        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .MinimumLevel.Error()
            .WriteTo.Console()
            .WriteTo.File(
                path: "Logs/schedule-api-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
            )
            .CreateLogger();

        builder.Host.UseSerilog();
    }

    public static void ConfigureOpenTelemetry(this WebApplicationBuilder builder)
    {
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService("StankinAppApi"))
            .WithTracing(t => t
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddSqlClientInstrumentation(o => o.SetDbStatementForText = true)
            )
            .WithMetrics(m => m
                .AddRuntimeInstrumentation()
                .AddHttpClientInstrumentation()
                .AddProcessInstrumentation()
                .AddPrometheusExporter()
            );
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
        builder.Services.AddHttpClient();

        var dbPath = configuration.GetValue<string>("Database:Path");
        if (string.IsNullOrEmpty(dbPath))
            throw new InvalidOperationException("Database path is not configured in appsettings.");

        var absoluteDbPath = Path.IsPathRooted(dbPath)
            ? dbPath
            : Path.Combine(builder.Environment.ContentRootPath, dbPath);

        builder.Services.AddSingleton<IDataReader>(_ => new DatabaseReader(absoluteDbPath));
        builder.Services.AddSingleton<IScheduleService, ScheduleService>();
    }

    public static void MapApi(this WebApplication app)
    {
        app.MapControllers();

        app.MapGet("/api/groups", (IScheduleService service, ILogger<Program> log) =>
        {
            log.LogInformation("GET /api/groups");
            var groups = service.GetGroups().ToList();
            return Results.Ok(new ListResponse<string>(groups));
        });

        app.MapGet("/api/rooms", (IScheduleService service, ILogger<Program> log) =>
        {
            log.LogInformation("GET /api/rooms");
            var rooms = service.GetRooms().ToList();
            return Results.Ok(new ListResponse<string>(rooms));
        });

        app.MapGet("/api/teachers", (IScheduleService service, ILogger<Program> log) =>
        {
            log.LogInformation("GET /api/teachers");
            var teachers = service.GetTeachers().ToList();
            return Results.Ok(new ListResponse<string>(teachers));
        });

        app.MapGet("/api/teachers/validate", (string name, IScheduleService service, ILogger<Program> log) =>
        {
            if (string.IsNullOrWhiteSpace(name))
                return Results.BadRequest(new { error = "Missing 'name' parameter" });

            var exists = service.GetTeachers().Contains(name, StringComparer.OrdinalIgnoreCase);
            log.LogInformation("Validated teacher '{TeacherName}': {Exists}", name, exists);
            return Results.Ok(new { exists });
        });

        app.MapGet("/api/schedule",
            (string groupName, string startDate, string endDate,
             IScheduleService service, IMemoryCache cache, ILogger<Program> log) =>
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

            var weekStart = parsedStart.AddDays(-(int)parsedStart.DayOfWeek == 0 ? 6 : (int)parsedStart.DayOfWeek - 1);

            var metadata = new ScheduleMetadata(
                NextWeek:    weekStart.AddDays(7).ToString("yyyy-MM-dd"),
                PrevWeek:    weekStart.AddDays(-7).ToString("yyyy-MM-dd"),
                PeriodStart: startDate,
                PeriodEnd:   endDate,
                IsLastWeek:  false
            );

            return Results.Ok(new ApiResponse<CourseDto>(metadata, lessons));
        });
    }
}
