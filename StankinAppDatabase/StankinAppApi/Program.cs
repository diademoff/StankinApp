namespace StankinAppApi
{
    class Program
    {
        private static readonly string PATH = Path.GetFullPath("schedule.db");

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

            app.MapGet("/api/groups", async (context) =>
            {
                var groups = db.GetGroups();
                await context.Response.WriteAsJsonAsync(groups);
            });

            app.MapGet("/api/rooms", async (context) =>
            {
                var groups = db.GetRooms();
                await context.Response.WriteAsJsonAsync(groups);
            });

            app.MapGet("/api/teachers", async (context) =>
            {
                var groups = db.GetTeachers();
                await context.Response.WriteAsJsonAsync(groups);
            });

            app.MapGet("/api/schedule", async (context) =>
            {
                var groupName = context.Request.Query["groupName"].ToString();
                var startDate = context.Request.Query["startDate"].ToString();
                var endDate = context.Request.Query["endDate"].ToString();

                if (string.IsNullOrEmpty(groupName) || string.IsNullOrEmpty(startDate) || string.IsNullOrEmpty(endDate))
                {
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsJsonAsync(new { error = "Missing required query parameters: groupName, startDate, endDate" });
                    return;
                }

                try
                {
                    var schedule = db.GetScheduleForGroupInRange(groupName, startDate, endDate);
                    await context.Response.WriteAsJsonAsync(schedule);
                }
                catch (Exception ex)
                {
                    context.Response.StatusCode = 500;
                    await context.Response.WriteAsJsonAsync(new { error = $"Failed to retrieve schedule: {ex.Message}" });
                }
            });

            app.Urls.Add("http://localhost:5001");
            app.Urls.Add("https://localhost:5002");

            app.RunAsync();

            Console.ReadLine();
        }
    }
}
