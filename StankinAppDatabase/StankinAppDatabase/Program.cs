using NodaTime;

namespace StankinAppDatabase
{
    public class Program
    {
        const string FOLDER_PATH = "D:\\repos\\StankinApp\\pdfparser\\json";
        const string DB_PATH = "schedule.db";
        public static int year;
        static ScheduleJsonReader _reader;

        static void Main(string[] args)
        {
            HandleErrorMethods.year = year;
            var HandleParseError = HandleErrorMethods.HandleParseError2025;
            DatabaseBuilder builder = new DatabaseBuilder(DateTime.Now.Year, HandleParseError, DB_PATH);

#if DEBUG
            // Check if database exists
            if (!File.Exists(DB_PATH))
            {
                Console.WriteLine("База данных не найдена. Создание новой базы данных...");

                Console.Write("Введите год расписания: ");
                year = Convert.ToInt32(Console.ReadLine());
                HandleErrorMethods.year = year;
                _reader = new ScheduleJsonReader(year, null);
                builder = new DatabaseBuilder(year, HandleParseError, DB_PATH);

                //Create database schema
                Console.WriteLine("Создание схемы базы данных...");
                builder.CreateSchema();

                //Process all JSON files and populate the database
                Console.WriteLine("Обработка JSON файлов...");
                builder.ProcessFolder(FOLDER_PATH);

                Console.WriteLine("База данных успешно создана!");
            }
            else
            {
                Console.WriteLine("База данных найдена.");
            }
#else
            if (!File.Exists(DB_PATH))
            {
                Console.WriteLine("Положите файл базы данных " + DB_PATH);
                Console.ReadLine();
                return;
            }
#endif
            // Start the UI
            var ui = new UI(new DatabaseReader(DB_PATH));
            ui.Run();
        }
    }
}