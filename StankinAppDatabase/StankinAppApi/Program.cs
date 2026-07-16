namespace StankinAppApi;

class Program
{
    static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.ConfigureLogging();
        builder.ConfigureKestrel();
        builder.ConfigureCors();
        builder.ConfigureServices();

        var app = builder.Build();

        app.UseCors("AllowFrontend");
        app.MapApi();

        builder.WebHost.UseUrls("http://0.0.0.0:5000");

        app.Run();
    }
}
