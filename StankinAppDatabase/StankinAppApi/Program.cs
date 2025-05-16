namespace StankinAppApi
{
    class Program
    {
        private static readonly string PATH = "D:\\schedule.db";

        static void Main(string[] args)
        {
            var appBuilder = WebApplication.CreateBuilder(args);

            appBuilder.WebHost.ConfigureKestrel(options =>
            {
                options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10 мб
                options.AddServerHeader = false; // убрать заголовок Server
            });
            var app = appBuilder.Build();

            var db = new StankinAppDatabase.DatabaseReader(PATH);

            app.MapGet("/groups", async (context) =>
            {
                var groups = db.GetGroups();
                await context.Response.WriteAsJsonAsync(groups);
            });

            app.MapGet("/rooms", async (context) =>
            {
                var groups = db.GetRooms();
                await context.Response.WriteAsJsonAsync(groups);
            });

            app.MapGet("/teachers", async (context) =>
            {
                var groups = db.GetTeachers();
                await context.Response.WriteAsJsonAsync(groups);
            });

            app.Urls.Add("http://localhost:5001");
            app.Urls.Add("https://localhost:5002");

            app.RunAsync();

            Console.ReadLine();
        }
    }
}
