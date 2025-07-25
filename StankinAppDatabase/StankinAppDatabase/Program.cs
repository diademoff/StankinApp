using NodaTime;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.ComponentModel.DataAnnotations;

namespace StankinAppDatabase
{
    public class Program
    {
        const string DB_PATH = "schedule.db";
        public static int year;
        static ScheduleJsonReader _reader;

        static void Main(string[] args)
        {
            var opt_path = new Option<string>(
                name: "--path"
            )
            {
                Description = "path to folder with json files"
            }.AcceptLegalFilePathsOnly();

            var opt_year = new Option<int>(
                name: "--year"
            )
            {
                Description = "year of provided json files"
            };

            var rootCommand = new RootCommand("StankinAppDatabase");
            rootCommand.Options.Add(opt_path);
            rootCommand.Options.Add(opt_year);

            ParseResult parseResult = rootCommand.Parse(args);

            foreach (ParseError parseError in parseResult.Errors)
            {
                Console.Error.WriteLine(parseError.Message);
            }

            if(parseResult.GetValue(opt_path) is string parsed_path &&
                parseResult.GetValue(opt_year) is int parsed_year)
            {
                Console.WriteLine($"Создание новой базы данных с переданными параметрами\n" +
                $"path = {parsed_path}\nyear = {parsed_year}");
                CreateDB(parsed_path, parsed_year);
                Environment.Exit(0);
            }


            // Check if database exists
            if (!File.Exists(DB_PATH))
            {
                Console.WriteLine("База данных не найдена.");
                Console.WriteLine("Положите файл базы данных " + DB_PATH);
            }
            else
            {
                Console.WriteLine("База данных найдена.");
            }

            // Start the UI
            var ui = new UI(new DatabaseReader(DB_PATH));
            ui.Run();
        }

        public static void CreateDB(string path, int year)
        {
            Console.WriteLine("База данных не найдена. Создание новой базы данных...");
            var HandleParseError = HandleErrorMethods.HandleParseError2025;
            HandleErrorMethods.year = year;
            DatabaseBuilder builder = new DatabaseBuilder(year, HandleParseError, DB_PATH);

            _reader = new ScheduleJsonReader(year, null);
            //Create database schema
            Console.WriteLine("Создание схемы базы данных...");
            builder.CreateSchema();
            //Process all JSON files and populate the database
            Console.WriteLine("Обработка JSON файлов...");
            builder.ProcessFolder(path);
            Console.WriteLine("База данных успешно создана!");
        }
    }
}