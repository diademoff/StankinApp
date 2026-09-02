namespace StankinAppApi;

using StankinAppApi.Diagnostics;

class Program
{
    static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.ConfigureLogging();
        builder.ConfigureKestrel();
        builder.ConfigureCors();
        builder.ConfigureServices();
        builder.Services.AddRequestRateLogging();

        var app = builder.Build();

        app.UseCors("AllowFrontend");
        app.UseRequestRateLogging();
        app.MapApi();
        app.MapBoardApi();

        app.Run();
    }
}
