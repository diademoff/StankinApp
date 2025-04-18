using NodaTime;

namespace StankinAppDatabase
{
    internal class Program
    {
        const string FOLDER_PATH = "D:\\repos\\StankinApp\\pdfparser\\json";
        const string DB_PATH = "schedule.db";

        static void Main(string[] args)
        {
            var builder = new DatabaseBuilder(2025, DB_PATH);

            // Check if database exists
            if (!File.Exists(DB_PATH))
            {
                Console.WriteLine("База данных не найдена. Создание новой базы данных...");
                
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

            // Start the UI
            var ui = new UI(builder);
            ui.Run();
        }
    }
}