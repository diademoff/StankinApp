using NUnit.Framework;
using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace StankinAppDatabase.Tests
{
    [TestFixture]
    public class ParserTests
    {
        private ScheduleJsonReader _reader;
        private NodaTime.LocalTime _time;
        private NodaTime.Period _period;
        private string _group;

        [SetUp]
        public void Setup()
        {
            _reader = new ScheduleJsonReader();
            _time = new NodaTime.LocalTime(12, 20);
            _period = NodaTime.Period.FromTicks(1000);
            _group = "grp";
        }

        [Test]
        public void ParseBasicLesson_ShouldReturnCorrectValues()
        {
            string input = "Компьютерная микроскопия. Шулепов А.В. лекции. 0309. [10.02-28.04 к.н.]";
            var output = _reader.ParseLessons(input, _time, _period, _group);

            Assert.That("Компьютерная микроскопия" == output[0].Subject);
            Assert.That("Шулепов А.В." == output[0].Teacher);
            Assert.That("лекции" == output[0].Type);
            Assert.That("0309" == output[0].Cabinet);
            Assert.That(string.IsNullOrEmpty(output[0].Subgroup));
        }

        [Test]
        public void ParseLabWorkWithSubgroups_ShouldReturnCorrectValues()
        {
            string input = "Разработка приложений для встраиваемых и мобильных устройств. Верещагин Н.М. лабораторные занятия. (А). 214. [19.02-19.03 ч.н.] " +
                          "Разработка приложений для встраиваемых и мобильных устройств. Верещагин Н.М. лабораторные занятия. (Б). 214. [26.02-26.03 ч.н.]";
            var output = _reader.ParseLessons(input, _time, _period, _group);

            Assert.That("Разработка приложений для встраиваемых и мобильных устройств" == output[0].Subject);
            Assert.That("Верещагин Н.М." == output[0].Teacher);
            Assert.That("лабораторные занятия" == output[0].Type);
            Assert.That("214" == output[0].Cabinet);
            Assert.That("А" == output[0].Subgroup);

            Assert.That("Разработка приложений для встраиваемых и мобильных устройств" == output[1].Subject);
            Assert.That("Верещагин Н.М." == output[1].Teacher);
            Assert.That("лабораторные занятия" == output[1].Type);
            Assert.That("214" == output[1].Cabinet);
            Assert.That("Б" == output[1].Subgroup);
        }

        [Test]
        public void ParseLabWorkWithSpecialCabinet_ShouldReturnCorrectValues()
        {
            string input = "Интеграция информационных систем и технологий. Тихомирова В.Д. лабораторные занятия. (А). 249(б). [26.03-23.04 ч.н., 14.05] " +
                          "Интеграция информационных систем и технологий. Тихомирова В.Д. лабораторные занятия. (Б). 249(б). [02.04-30.04 ч.н., 21.05]";
            var output = _reader.ParseLessons(input, _time, _period, _group);

            Assert.That("Интеграция информационных систем и технологий" == output[0].Subject);
            Assert.That("Тихомирова В.Д." == output[0].Teacher);
            Assert.That("лабораторные занятия" == output[0].Type);
            Assert.That("249(б)" == output[0].Cabinet);
            Assert.That("А" == output[0].Subgroup);

            Assert.That("Интеграция информационных систем и технологий" == output[1].Subject);
            Assert.That("Тихомирова В.Д." == output[1].Teacher);
            Assert.That("лабораторные занятия" == output[1].Type);
            Assert.That("249(б)" == output[1].Cabinet);
            Assert.That("Б" == output[1].Subgroup);
        }

        [Test]
        public void ParseSeminarWithoutCabinet_ShouldReturnCorrectValues()
        {
            string input = "Экономика и управление машиностроительным производством. Гайбу В. . семинар. . [21.02-28.03 к.н.]";
            var output = _reader.ParseLessons(input, _time, _period, _group);

            Assert.That("Экономика и управление машиностроительным производством" == output[0].Subject);
            Assert.That("Гайбу В." == output[0].Teacher);
            Assert.That("семинар" == output[0].Type);
            Assert.That(string.IsNullOrEmpty(output[0].Cabinet));
            Assert.That(string.IsNullOrEmpty(output[0].Subgroup));
        }

        [Test]
        public void ParseLabWorkWithMultipleDates_ShouldReturnCorrectValues()
        {
            string input = "Информатика. Кобец Е. . лабораторные занятия. (А). 210. [21.03-18.04 ч.н., 16.05, 30.05] " +
                          "Информатика. Кобец Е. . лабораторные занятия. (Б). 210. [28.03-25.04 ч.н., 23.05, 06.06]";
            var output = _reader.ParseLessons(input, _time, _period, _group);

            Assert.That("Информатика" == output[0].Subject);
            Assert.That("Кобец Е." == output[0].Teacher);
            Assert.That("лабораторные занятия" == output[0].Type);
            Assert.That("210" == output[0].Cabinet);
            Assert.That("А" == output[0].Subgroup);

            Assert.That("Информатика" == output[1].Subject);
            Assert.That("Кобец Е." == output[1].Teacher);
            Assert.That("лабораторные занятия" == output[1].Type);
            Assert.That("210" == output[1].Cabinet);
            Assert.That("Б" == output[1].Subgroup);
        }

        [Test]
        public void ParseSeminarWithLongTeacherName_ShouldReturnCorrectValues()
        {
            string input = "Введение в проектную деятельность: технологическое и социальное проектирование. Амир Абдаллах Д. А.  . . семинар. 443. [19.02-07.05 к.н.]";
            var output = _reader.ParseLessons(input, _time, _period, _group);

            Assert.That("Введение в проектную деятельность: технологическое и социальное проектирование" == output[0].Subject);
            Assert.That("Амир Абдаллах Д. А." == output[0].Teacher);
            Assert.That("семинар" == output[0].Type);
            Assert.That("443" == output[0].Cabinet);
            Assert.That(string.IsNullOrEmpty(output[0].Subgroup));
        }

        [Test]
        public void ParseSeminarWithSpecialTeacherName_ShouldReturnCorrectValues()
        {
            string input = "Модели и методы теории вычислений. Агоштинью Адау Какулу . . семинар. . [01.03, 15.03-29.03 к.н.]";
            var output = _reader.ParseLessons(input, _time, _period, _group);

            Assert.That("Модели и методы теории вычислений" == output[0].Subject);
            Assert.That("Агоштинью Адау Какулу" == output[0].Teacher);
            Assert.That("семинар" == output[0].Type);
            Assert.That(string.IsNullOrEmpty(output[0].Cabinet));
            Assert.That(string.IsNullOrEmpty(output[0].Subgroup));
        }

        [Test]
        public void ParseMultipleLessonsWithDifferentTypes_ShouldReturnCorrectValues()
        {
            string input = "Физика. Аристархов П.В. лабораторные занятия. (А). 419. [20.02-03.04 ч.н.] " +
                          "Физика. Аристархов П.В. лабораторные занятия. (Б). 419. [27.02-10.04 ч.н.] " +
                          "Информатика. Кобец Е. . лабораторные занятия. (Б). 214. [20.03-17.04 ч.н., 15.05, 29.05] " +
                          "Информатика. Кобец Е. . лабораторные занятия. (А). 214. [27.03-24.04 ч.н., 22.05, 05.06]";
            var output = _reader.ParseLessons(input, _time, _period, _group);

            Assert.That("Физика" == output[0].Subject);
            Assert.That("Аристархов П.В." == output[0].Teacher);
            Assert.That("лабораторные занятия" == output[0].Type);
            Assert.That("419" == output[0].Cabinet);
            Assert.That("А" == output[0].Subgroup);

            Assert.That("Физика" == output[1].Subject);
            Assert.That("Аристархов П.В." == output[1].Teacher);
            Assert.That("лабораторные занятия" == output[1].Type);
            Assert.That("419" == output[1].Cabinet);
            Assert.That("Б" == output[1].Subgroup);

            Assert.That("Информатика" == output[2].Subject);
            Assert.That("Кобец Е." == output[2].Teacher);
            Assert.That("лабораторные занятия" == output[2].Type);
            Assert.That("214" == output[2].Cabinet);
            Assert.That("Б" == output[2].Subgroup);

            Assert.That("Информатика" == output[3].Subject);
            Assert.That("Кобец Е." == output[3].Teacher);
            Assert.That("лабораторные занятия" == output[3].Type);
            Assert.That("214" == output[3].Cabinet);
            Assert.That("А" == output[3].Subgroup);
        }
    }
}