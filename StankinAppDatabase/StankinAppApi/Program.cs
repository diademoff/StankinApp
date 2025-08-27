namespace StankinAppApi;

class Program
{
    static string IP = "89.111.131.170";
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

        // app.Urls.Add($"http://{IP}:5001");
        // app.Urls.Add($"https://{IP}:5002");
        builder.WebHost.UseUrls("http://0.0.0.0:5000");

        app.Run();
    }
}