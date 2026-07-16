using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Serilog;
using StankinAppApi.Dto;
using StankinAppCore;

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
    }

    public static void MapApi(this WebApplication app)
    {
        app.MapControllers();

        app.MapGet("/api/groups", (ScheduleService service, ILogger<Program> log) =>
        {
            log.LogInformation("GET /api/groups");
            var groups = service.GetGroups().ToList();
            return Results.Ok(new ListResponse<string>(groups));
        });

        app.MapGet("/api/rooms", (ScheduleService service, ILogger<Program> log) =>
        {
            log.LogInformation("GET /api/rooms");
            var rooms = service.GetRooms().ToList();
            return Results.Ok(new ListResponse<string>(rooms));
        });

        app.MapGet("/api/teachers", (ScheduleService service, ILogger<Program> log) =>
        {
            log.LogInformation("GET /api/teachers");
            var teachers = service.GetTeachers().ToList();
            return Results.Ok(new ListResponse<string>(teachers));
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
            IDataReader reader) =>
        {
            try
            {
                var courses = reader.GetScheduleForTeacher(teacherName, startDate, endDate);

                if (!courses.Any()) return Results.NoContent();

                var dtos = courses.Select(c => new CourseDto(
                    Id: $"{c.GroupName}_{c.Dates[0].ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}_{c.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture)}_{c.Subgroup ?? "all"}",
                    Date: c.Dates[0].ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    StartTime: c.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture),
                    EndTime: (c.StartTime + c.Duration).ToString("HH:mm", CultureInfo.InvariantCulture),
                    DurationMinutes: (int)c.Duration.ToDuration().TotalMinutes,
                    GroupName: c.GroupName,
                    Subject: c.Subject,
                    Teacher: c.Teacher,
                    Type: c.Type,
                    Subgroup: c.Subgroup ?? "",
                    Cabinet: c.Cabinet ?? "",
                    SequencePosition: c.SequencePosition,
                    SequenceLength: c.SequenceLength
                ));

                return Results.Ok(new ListResponse<CourseDto>(dtos));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        });
    }
}
