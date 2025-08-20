namespace StankinAppApi;

class Program
{
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

        app.Urls.Add("http://192.168.0.103:5001");
        app.Urls.Add("https://192.168.0.103:5002");

        app.Run();
    }
}