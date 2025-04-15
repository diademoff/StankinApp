using NodaTime;

namespace StankinAppDatabase
{
    internal class Program
    {
        const string FOLDER_PATH = "D:\\repos\\StankinApp\\pdfparser\\json";
        const string DB_PATH = "schedule.db";

        static void Main(string[] args)
        {
            var builder = new DatabaseBuilder(DB_PATH);

            //Create database schema
            //Console.WriteLine("Creating database schema...");
            //builder.CreateSchema();

            //Process all JSON files and populate the database
            //Console.WriteLine("Processing JSON files...");
            //builder.ProcessFolder(FOLDER_PATH);

            //Console.WriteLine("Database creation completed!");


            //убедись, что схема создана, если нужно
            builder.CreateSchema();
            //используя GetGroups, берем первую группу
            var grp = builder.GetGroups();

            // год тут уже не важен, но LocalDate требует год — оставляем его просто для создания объекта даты
            var courses = builder.GetScheduleForGroup(grp[23], new LocalDate(2000, 4, 16));

            foreach (var course in courses)
            {
                Console.WriteLine(course.ToString());
            }

            Console.WriteLine("End!");
            Console.ReadKey();
        }
    }
}