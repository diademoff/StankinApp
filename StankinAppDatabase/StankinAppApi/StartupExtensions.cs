using Serilog;
using StankinAppCore;
using Microsoft.Extensions.Caching.Memory;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
namespace StankinAppApi;

static class StartupExtensions
{
    static string[] AvailableIp = [
        "https://stankinapp.ru",
        "https://www.stankinapp.ru",
        "https://89.111.131.170",
        "http://89.111.131.170"
    ];
    public static void ConfigureLogging(this WebApplicationBuilder builder)
    {
        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .MinimumLevel.Debug()
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
            p.AllowAnyOrigin()
        ));
#else
        builder.Services.AddCors(o => o.AddPolicy("AllowFrontend", p =>
            p.WithOrigins(AvailableIp)
                .AllowAnyHeader()
                .AllowAnyMethod()
        ));
#endif
    }

    public static void ConfigureServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddMemoryCache();
        builder.Services.AddSingleton<IDataReader>(_ =>
            new DatabaseReader(Path.GetFullPath("schedule.db"))
        );
    }

    public static void MapApi(this WebApplication app)
    {
        app.MapGet("/api/groups", (IDataReader db, ILogger<Program> log) =>
        {
            log.LogInformation("GET /api/groups");
            return Results.Json(db.GetGroups());
        });

        app.MapGet("/api/rooms", (IDataReader db, ILogger<Program> log) =>
        {
            log.LogInformation("GET /api/rooms");
            return Results.Json(db.GetRooms());
        });

        app.MapGet("/api/teachers", (IDataReader db, ILogger<Program> log) =>
        {
            log.LogInformation("GET /api/teachers");
            return Results.Json(db.GetTeachers());
        });

        app.MapGet("/api/schedule", (string groupName, string startDate, string endDate,
                                     IDataReader db, IMemoryCache cache, ILogger<Program> log) =>
        {
            if (string.IsNullOrWhiteSpace(groupName) ||
                string.IsNullOrWhiteSpace(startDate) ||
                string.IsNullOrWhiteSpace(endDate))
            {
                log.LogWarning("Missing parameters: group={Group}, start={Start}, end={End}",
                    groupName, startDate, endDate);
                return Results.BadRequest(new { error = "Missing required parameters" });
            }

            var key = $"sched:{groupName}:{startDate}:{endDate}";
            if (!cache.TryGetValue(key, out var schedule))
            {
                try
                {
                    schedule = db.GetScheduleForGroup(groupName, startDate, endDate)
                                 .Select(x => new CourseDto(x));
                    cache.Set(key, schedule, TimeSpan.FromHours(2));
                    log.LogInformation("Fetched from DB & cached: {Key}", key);
                }
                catch (Exception ex)
                {
                    log.LogError(ex, "Error fetching schedule for {Group}", groupName);
                    return Results.Json(new { error = "Internal server error" }, statusCode: 500);
                }
            }
            else
            {
                log.LogInformation("Served from cache: {Key}", key);
            }

            return Results.Json(schedule);
        });
    }
}