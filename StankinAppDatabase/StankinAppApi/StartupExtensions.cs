using Serilog;
using StankinAppCore;
using Microsoft.Extensions.Caching.Memory;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using StankinAppApi.Services;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace StankinAppApi;

static class StartupExtensions
{
    static string[] AvailableIp = [
        "https://stankinapp.ru",
        "http://stankinapp.ru",
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
                .AllowAnyHeader()
                .AllowAnyMethod()
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
        var configuration = builder.Configuration;

        // Add controllers support
        builder.Services.AddControllers();

        // Add memory cache
        builder.Services.AddMemoryCache();

        builder.Services.AddHttpClient(); // Для HttpClientFactory
        builder.Services.AddSingleton<IAuthService, AuthService>();
        builder.Services.AddSingleton<IRatingService, RatingService>();

        // Configure Authentication
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                var jwtConfig = builder.Configuration.GetSection("Jwt");
                options.TokenValidationParameters = new()
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtConfig["Issuer"],
                    ValidAudience = jwtConfig["Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtConfig["Secret"]!))
                };
            });

        // Add Authorization
        builder.Services.AddAuthorization();

        // Read database path from configuration (appsettings.json or appsettings.Development.json)
        var dbPath = configuration.GetValue<string>("Database:Path");
        if (string.IsNullOrEmpty(dbPath))
        {
            throw new InvalidOperationException("Database path is not configured in appsettings.");
        }

        // Make path relative to the application's root directory if it's not already an absolute path
        var absoluteDbPath = Path.IsPathRooted(dbPath)
            ? dbPath
            : Path.Combine(builder.Environment.ContentRootPath, dbPath);

        builder.Services.AddSingleton<IDataReader>(_ =>
            new DatabaseReader(absoluteDbPath)
        );

        // Register existing services
        builder.Services.AddSingleton<IDataReader>(_ =>
            new DatabaseReader(Path.GetFullPath(dbPath))
        );

        builder.Services.AddSingleton<IScheduleService, ScheduleService>();

        builder.Services.AddControllers();
        builder.Services.AddMemoryCache();

        // Добавлено для HTTP-запросов к Yandex API
        builder.Services.AddHttpClient();

        // Register new services for rating system
        builder.Services.AddSingleton<IAuthService, AuthService>();
        builder.Services.AddSingleton<IRatingService, RatingService>();
    }

    public static void MapApi(this WebApplication app)
    {
        // Add authentication/authorization middleware
        app.UseAuthentication();
        app.UseAuthorization();

        // Map controllers
        app.MapControllers();

        // Keep existing endpoints
        app.MapGet("/api/groups", (IScheduleService service, ILogger<Program> log) =>
        {
            log.LogInformation("GET /api/groups");
            return Results.Json(service.GetGroups());
        });

        app.MapGet("/api/rooms", (IScheduleService service, ILogger<Program> log) =>
        {
            log.LogInformation("GET /api/rooms");
            return Results.Json(service.GetRooms());
        });

        app.MapGet("/api/teachers", (IScheduleService service, ILogger<Program> log) =>
        {
            log.LogInformation("GET /api/teachers");
            return Results.Json(service.GetTeachers());
        });

        app.MapGet("/api/schedule", (string groupName, string startDate, string endDate,
                                     IScheduleService service, IMemoryCache cache, ILogger<Program> log) =>
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
                    schedule = service.GetMergedScheduleForGroup(groupName, startDate, endDate);
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

        app.MapGet("/api/teachers/validate", (string name, IScheduleService service, ILogger<Program> log) =>
        {
            if (string.IsNullOrWhiteSpace(name))
                return Results.BadRequest(new { error = "Missing 'name' parameter" });

            var exists = service.GetTeachers().Contains(name, StringComparer.OrdinalIgnoreCase);
            log.LogInformation("Validated teacher '{TeacherName}': {Exists}", name, exists);
            return Results.Json(new { exists });
        });
    }
}