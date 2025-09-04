using System.CommandLine;
using StankinAppCore;

namespace StankinAppDatabase
{
    public class Program
    {
        const string FALLBACK_PATH = "fallback_info.json";

        static async Task<int> Main(string[] args)
        {
            // Примеры использования
            //args = new string[] 
            //{
            //    "read",
            //    "--db-path",
            //    "D:\\repos\\schedule.db"
            //};
            //args = new string[]
            //{
            //    "--help"
            //};
            //args = new string[]
            //{   
            //    "create",
            //    "--help"
            //};
            //args = new string[]
            //{   
            //    "read",
            //    "--help"
            //};
            //args = new string[] 
            //{
            //    "create",
            //    "--json-path",
            //    "D:\\repos\\json",
            //    "--year",
            //    "2025",
            //    // Optional
            //    "--db-save-path",
            //    "D:\\database.db"
            //};

            var rootCommand = new RootCommand("Утилита для работы с базой данных расписания");

            // Команда создания БД
            var jsonPathOption = new Option<DirectoryInfo>(
                name: "--json-path")
            {
                Description = "Путь к папке с JSON файлами",
                Required = true
            }.AcceptExistingOnly();

            var yearOption = new Option<int>(
                name: "--year")
            {
                Description = "Год обрабатываемых JSON файлов",
                Required = true
            };

            var dbSavePathOption = new Option<FileInfo>(
                name: "--db-save-path")
            {
                Description = "Путь для сохранения БД (по умолчанию schedule.db)",
                DefaultValueFactory = _ => new FileInfo("schedule.db")
            };

            var createCommand = new Command("create", "Создание БД из JSON файлов")
            {
                jsonPathOption,
                yearOption,
                dbSavePathOption
            };

            createCommand.SetAction(args =>
            {
                CreateDatabase(args.GetValue(jsonPathOption), args.GetValue(yearOption), args.GetValue(dbSavePathOption));
            });

            // Команда чтения БД
            var dbOpenPathOption = new Option<FileInfo>(
                name: "--db-path")
            {
                Description = "Путь к файлу БД для чтения",
                Required = true
            }.AcceptExistingOnly();

            var readCommand = new Command("read", "Чтение существующей БД")
            {
                dbOpenPathOption
            };
            readCommand.SetAction(args => 
            {
                ReadDatabase(args.GetValue(dbOpenPathOption));
            });

            rootCommand.Subcommands.Add(createCommand);
            rootCommand.Subcommands.Add(readCommand);

            return await rootCommand.Parse(args).InvokeAsync();
        }

        static void CreateDatabase(DirectoryInfo jsonPath, int year, FileInfo dbSavePath)
        {
            Console.WriteLine($"Создание новой базы данных:\n" +
                $"JSON Path: {jsonPath}\n" +
                $"Year: {year}\n" +
                $"DB Save Path: {dbSavePath}");

            ParserFallback fallback = new(year, FALLBACK_PATH);
            var handleParseError = fallback.ParseFallbackFunc;

            var builder = new DatabaseScraper(year, handleParseError, dbSavePath.FullName);

            Console.WriteLine("Создание схемы базы данных...");
            builder.CreateSchema();

            Console.WriteLine("Обработка JSON файлов...");
            builder.ProcessFolder(jsonPath.FullName);

            Console.WriteLine("База данных успешно создана!");
        }

        static void ReadDatabase(FileInfo dbPath)
        {
            Console.WriteLine($"Чтение базы данных: {dbPath}");

            if (!dbPath.Exists)
            {
                Console.Error.WriteLine("Ошибка: Файл базы данных не существует");
                return;
            }

            var ui = new UI(new DatabaseReader(dbPath.FullName));
            ui.Run();
        }
    }
}