using NUnit.Framework;
using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace StankinApp.Core.Tests
{
    [TestFixture]
    public class ParserTests
    {
        string folderDataLocation;
        [SetUp]
        public void SetUp()
        {
            string currentPath = Assembly.GetExecutingAssembly().Location;
            folderDataLocation = Path.GetFullPath(Path.Combine(currentPath, @"..\..\..\..\data\"));
        }

        [Test]
        public void TestParse1()
        {
            string fileName = "АДБ-23-07.json";
            string fullPath = Path.Combine(folderDataLocation, fileName);

            string content = File.ReadAllText(fullPath);

            var data = ScheduleJsonReader.GetSchedule(content);

            using (var stream = new FileStream(fileName + ".log.json", FileMode.CreateNew))
                using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
                {
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                }))
                    JsonSerializer.Serialize(writer, data);


            Assert.Multiple(() =>
            {
                // Получено дней
                Assert.That(data.Days, Has.Count.EqualTo(6));

                // Количество пар
                Assert.That(data.Days[0].TimeSlots, Has.Count.EqualTo(4));
                Assert.That(data.Days[1].TimeSlots, Has.Count.EqualTo(6));
                Assert.That(data.Days[2].TimeSlots, Has.Count.EqualTo(4));
                Assert.That(data.Days[3].TimeSlots, Has.Count.EqualTo(4));
                Assert.That(data.Days[4].TimeSlots, Has.Count.EqualTo(4));
                Assert.That(data.Days[5].TimeSlots, Has.Count.EqualTo(3));
            });
        }
    }
}
