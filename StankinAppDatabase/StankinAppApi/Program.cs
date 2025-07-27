using Microsoft.Extensions.Caching.Memory;
using Serilog;
using StankinAppCore;

var builder = WebApplication.CreateBuilder(args);

// 1) Серилог: консоль + файл (ротация по дням, хранить 7 файлов)
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

// 2) Kestrel
builder.WebHost.ConfigureKestrel(opts =>
{
    opts.Limits.MaxRequestBodySize = 10 * 1024 * 1024;
    opts.AddServerHeader = false;
});

// 3) CORS
var origins = new[]
{
    "http://127.0.0.1:5500" // dev front-end
};

builder.Services.AddCors(o => o.AddPolicy("AllowFrontend", p =>
    p.WithOrigins(origins)
     .AllowAnyHeader()
     .AllowAnyMethod()
));

// 4) DI: MemoryCache + IDataReader
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IDataReader>(_ =>
    new DatabaseReader(Path.GetFullPath("schedule.db"))
);

var app = builder.Build();
app.UseCors("AllowFrontend");

// 5) Endpoints

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
            schedule = db.GetScheduleForGroup(groupName, startDate, endDate);
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

app.Urls.Add("http://localhost:5001");
app.Urls.Add("https://localhost:5002");

app.Run();
