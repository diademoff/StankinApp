using StankinAppCore;
namespace StankinAppDatabase;

public class DatabaseScraper
{
    private readonly ScheduleJsonReader _scheduleReader;
    private readonly int _currentYear;
    private readonly DatabaseBuilder _databaseBuilder;

    public DatabaseScraper(int currentYear, Func<ErrorParsingInfo, Course[]> parseError, string dbPath = "schedule.db")
    {
        _scheduleReader = new ScheduleJsonReader(currentYear, parseError);
        _currentYear = currentYear;
        _databaseBuilder = new DatabaseBuilder(dbPath);
    }

    public void CreateSchema()
    {
        _databaseBuilder.CreateSchema();
    }

    public void ProcessJsonFile(string filePath)
    {
        var json = File.ReadAllText(filePath);
        var groupName = Path.GetFileNameWithoutExtension(filePath);
        var schedule = _scheduleReader.GetSchedule(groupName, json);

        _databaseBuilder.InsertGroupSchedule(groupName, schedule.Days, _currentYear);
    }

    public void ProcessFolder(string folderPath)
    {
        foreach (var file in Directory.GetFiles(folderPath, "*.json"))
        {
            try
            {
                Console.WriteLine($"processing file: {file}");
                ProcessJsonFile(file);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"error processing {file}: {ex.Message}");
            }
        }
        Console.WriteLine("database population completed!");
    }
}