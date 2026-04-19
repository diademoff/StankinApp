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
        var files = Directory.GetFiles(folderPath, "*.json");
        for(int i = 0; i < files.Length; i++)
        {
            var file = files[i];
            try
            {
                Console.WriteLine($"Обработка файла: {Path.GetFileName(file)} [{i + 1}/{files.Length}]");
                ProcessJsonFile(file);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка обработки файла {file}: {ex.Message}");
            }
        }
        Console.WriteLine("Заполнение базы данных завершено!");
    }
}