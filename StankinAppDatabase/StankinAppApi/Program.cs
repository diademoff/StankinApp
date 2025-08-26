namespace StankinAppApi;

class Program
{
    static string IP = "192.168.1.210";
    static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.ConfigureLogging();
        builder.ConfigureOpenTelemetry();
        builder.ConfigureKestrel();
        builder.ConfigureCors();
        builder.ConfigureServices();

        var app = builder.Build();

        // TODO: close /metrics
        app.UseOpenTelemetryPrometheusScrapingEndpoint();
        app.UseCors("AllowFrontend");
        app.MapApi();

        app.Urls.Add($"http://{IP}:5001");
        app.Urls.Add($"https://{IP}:5002");

        app.Run();
    }
}