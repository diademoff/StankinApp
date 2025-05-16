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
            Program.year = 2025;
            _reader = new ScheduleJsonReader(2025, Program.HandleParseError);
            _time = new NodaTime.LocalTime(12, 20);
            _period = NodaTime.Period.FromTicks(1000);
            _group = "grp";
        }

        [Test]
        public void ParseBasicLesson_ShouldReturnCorrectValues()
        {
            string input = "Компьютерная микроскопия. Шулепов А.В. лекции. 0309. [10.02-28.04 к.н.]";
            var output = _reader.ParseLessons(input, _time, _period, _group);

            Assert.Multiple(() =>
            {
                Assert.That(output[0].Subject, Is.EqualTo("Компьютерная микроскопия"));
                Assert.That(output[0].Teacher, Is.EqualTo("Шулепов А.В."));
                Assert.That(output[0].Type, Is.EqualTo("лекции"));
                Assert.That(output[0].Cabinet, Is.EqualTo("0309"));
                Assert.That(string.IsNullOrEmpty(output[0].Subgroup));
            });
        }

        [Test]
        public void ParseLabWorkWithSubgroups_ShouldReturnCorrectValues()
        {
            string input = "Разработка приложений для встраиваемых и мобильных устройств. Верещагин Н.М. лабораторные занятия. (А). 214. [19.02-19.03 ч.н.] " +
                          "Разработка приложений для встраиваемых и мобильных устройств. Верещагин Н.М. лабораторные занятия. (Б). 214. [26.02-26.03 ч.н.]";
            var output = _reader.ParseLessons(input, _time, _period, _group);

            Assert.Multiple(() =>
            {
                Assert.That(output[0].Subject, Is.EqualTo("Разработка приложений для встраиваемых и мобильных устройств"));
                Assert.That(output[0].Teacher, Is.EqualTo("Верещагин Н.М."));
                Assert.That(output[0].Type, Is.EqualTo("лабораторные занятия"));
                Assert.That(output[0].Cabinet, Is.EqualTo("214"));
                Assert.That(output[0].Subgroup, Is.EqualTo("А"));

                Assert.That(output[1].Subject, Is.EqualTo("Разработка приложений для встраиваемых и мобильных устройств"));
                Assert.That(output[1].Teacher, Is.EqualTo("Верещагин Н.М."));
                Assert.That(output[1].Type, Is.EqualTo("лабораторные занятия"));
                Assert.That(output[1].Cabinet, Is.EqualTo("214"));
                Assert.That(output[1].Subgroup, Is.EqualTo("Б"));
            });
        }

        [Test]
        public void ParseLabWorkWithSpecialCabinet_ShouldReturnCorrectValues()
        {
            string input = "Интеграция информационных систем и технологий. Тихомирова В.Д. лабораторные занятия. (А). 249(б). [26.03-23.04 ч.н., 14.05] " +
                          "Интеграция информационных систем и технологий. Тихомирова В.Д. лабораторные занятия. (Б). 249(б). [02.04-30.04 ч.н., 21.05]";
            var output = _reader.ParseLessons(input, _time, _period, _group);

            Assert.Multiple(() =>
            {
                Assert.That(output[0].Subject, Is.EqualTo("Интеграция информационных систем и технологий"));
                Assert.That(output[0].Teacher, Is.EqualTo("Тихомирова В.Д."));
                Assert.That(output[0].Type, Is.EqualTo("лабораторные занятия"));
                Assert.That(output[0].Cabinet, Is.EqualTo("249(б)"));
                Assert.That(output[0].Subgroup, Is.EqualTo("А"));

                Assert.That(output[1].Subject, Is.EqualTo("Интеграция информационных систем и технологий"));
                Assert.That(output[1].Teacher, Is.EqualTo("Тихомирова В.Д."));
                Assert.That(output[1].Type, Is.EqualTo("лабораторные занятия"));
                Assert.That(output[1].Cabinet, Is.EqualTo("249(б)"));
                Assert.That(output[1].Subgroup, Is.EqualTo("Б"));
            });
        }

        [Test]
        public void ParseSeminarWithoutCabinet_ShouldReturnCorrectValues()
        {
            string input = "Экономика и управление машиностроительным производством. Гайбу В. . семинар. . [21.02-28.03 к.н.]";
            var output = _reader.ParseLessons(input, _time, _period, _group);

            Assert.Multiple(() =>
            {
                Assert.That(output[0].Subject, Is.EqualTo("Экономика и управление машиностроительным производством"));
                Assert.That(output[0].Teacher, Is.EqualTo("Гайбу В."));
                Assert.That(output[0].Type, Is.EqualTo("семинар"));
                Assert.That(string.IsNullOrEmpty(output[0].Cabinet));
                Assert.That(string.IsNullOrEmpty(output[0].Subgroup));
            });
        }

        [Test]
        public void ParseLabWorkWithMultipleDates_ShouldReturnCorrectValues()
        {
            string input = "Информатика. Кобец Е. . лабораторные занятия. (А). 210. [21.03-18.04 ч.н., 16.05, 30.05] " +
                          "Информатика. Кобец Е. . лабораторные занятия. (Б). 210. [28.03-25.04 ч.н., 23.05, 06.06]";
            var output = _reader.ParseLessons(input, _time, _period, _group);

            Assert.Multiple(() =>
            {
                Assert.That(output[0].Subject, Is.EqualTo("Информатика"));
                Assert.That(output[0].Teacher, Is.EqualTo("Кобец Е."));
                Assert.That(output[0].Type, Is.EqualTo("лабораторные занятия"));
                Assert.That(output[0].Cabinet, Is.EqualTo("210"));
                Assert.That(output[0].Subgroup, Is.EqualTo("А"));

                Assert.That(output[1].Subject, Is.EqualTo("Информатика"));
                Assert.That(output[1].Teacher, Is.EqualTo("Кобец Е."));
                Assert.That(output[1].Type, Is.EqualTo("лабораторные занятия"));
                Assert.That(output[1].Cabinet, Is.EqualTo("210"));
                Assert.That(output[1].Subgroup, Is.EqualTo("Б"));
            });
        }

        [Test]
        public void ParseSeminarWithLongTeacherName_ShouldReturnCorrectValues()
        {
            string input = "Введение в проектную деятельность: технологическое и социальное проектирование. Амир Абдаллах Д. А.  . . семинар. 443. [19.02-07.05 к.н.]";
            var output = _reader.ParseLessons(input, _time, _period, _group);

            Assert.Multiple(() =>
            {
                Assert.That(output[0].Subject, Is.EqualTo("Введение в проектную деятельность: технологическое и социальное проектирование"));
                Assert.That(output[0].Teacher, Is.EqualTo("Амир Абдаллах Д. А."));
                Assert.That(output[0].Type, Is.EqualTo("семинар"));
                Assert.That(output[0].Cabinet, Is.EqualTo("443"));
                Assert.That(string.IsNullOrEmpty(output[0].Subgroup));
            });
        }

        [Test]
        public void ParseSeminarWithSpecialTeacherName_ShouldReturnCorrectValues()
        {
            string input = "Модели и методы теории вычислений. Агоштинью Адау Какулу . . семинар. . [01.03, 15.03-29.03 к.н.]";
            var output = _reader.ParseLessons(input, _time, _period, _group);

            Assert.Multiple(() =>
            {
                Assert.That(output[0].Subject, Is.EqualTo("Модели и методы теории вычислений"));
                Assert.That(output[0].Teacher, Is.EqualTo("Агоштинью Адау Какулу"));
                Assert.That(output[0].Type, Is.EqualTo("семинар"));
                Assert.That(string.IsNullOrEmpty(output[0].Cabinet));
                Assert.That(string.IsNullOrEmpty(output[0].Subgroup));
            });
        }

        [Test]
        public void ParseMultipleLessonsWithDifferentTypes_ShouldReturnCorrectValues()
        {
            string input = "Физика. Аристархов П.В. лабораторные занятия. (А). 419. [20.02-03.04 ч.н.] " +
                          "Физика. Аристархов П.В. лабораторные занятия. (Б). 419. [27.02-10.04 ч.н.] " +
                          "Информатика. Кобец Е. . лабораторные занятия. (Б). 214. [20.03-17.04 ч.н., 15.05, 29.05] " +
                          "Информатика. Кобец Е. . лабораторные занятия. (А). 214. [27.03-24.04 ч.н., 22.05, 05.06]";
            var output = _reader.ParseLessons(input, _time, _period, _group);

            Assert.Multiple(() =>
            {
                Assert.That(output[0].Subject, Is.EqualTo("Физика"));
                Assert.That(output[0].Teacher, Is.EqualTo("Аристархов П.В."));
                Assert.That(output[0].Type, Is.EqualTo("лабораторные занятия"));
                Assert.That(output[0].Cabinet, Is.EqualTo("419"));
                Assert.That(output[0].Subgroup, Is.EqualTo("А"));

                Assert.That(output[1].Subject, Is.EqualTo("Физика"));
                Assert.That(output[1].Teacher, Is.EqualTo("Аристархов П.В."));
                Assert.That(output[1].Type, Is.EqualTo("лабораторные занятия"));
                Assert.That(output[1].Cabinet, Is.EqualTo("419"));
                Assert.That(output[1].Subgroup, Is.EqualTo("Б"));

                Assert.That(output[2].Subject, Is.EqualTo("Информатика"));
                Assert.That(output[2].Teacher, Is.EqualTo("Кобец Е."));
                Assert.That(output[2].Type, Is.EqualTo("лабораторные занятия"));
                Assert.That(output[2].Cabinet, Is.EqualTo("214"));
                Assert.That(output[2].Subgroup, Is.EqualTo("Б"));

                Assert.That(output[3].Subject, Is.EqualTo("Информатика"));
                Assert.That(output[3].Teacher, Is.EqualTo("Кобец Е."));
                Assert.That(output[3].Type, Is.EqualTo("лабораторные занятия"));
                Assert.That(output[3].Cabinet, Is.EqualTo("214"));
                Assert.That(output[3].Subgroup, Is.EqualTo("А"));
            });
        }

        [Test]
        public void ParseDateTest1()
        {
            string input = "";
            var output = _reader.ParseSchedule("10.02", 2025);

            Assert.Multiple(() =>
            {
                Assert.That(output.Count, Is.EqualTo(1));
                Assert.That(output, Contains.Item(new NodaTime.LocalDate(2025, 02, 10)));
            });
        }

        [Test]
        public void ParseDateTest2()
        {
            string input = "";
            var output = _reader.ParseSchedule("10.02, 13.02", 2025);

            Assert.Multiple(() =>
            {
                Assert.That(output.Count(), Is.EqualTo(2));
                Assert.That(output, Contains.Item(new NodaTime.LocalDate(2025, 02, 10)));
                Assert.That(output, Contains.Item(new NodaTime.LocalDate(2025, 02, 13)));
            });
        }

        [Test]
        public void ParseDateTest3()
        {
            string input = "";
            var output = _reader.ParseSchedule("10.02, 24.02-05.05 к.н.", 2025);

            Assert.Multiple(() =>
            {
                Assert.That(output.Count(), Is.EqualTo(12));
                Assert.That(output, Contains.Item(new NodaTime.LocalDate(2025, 02, 10)));
                Assert.That(output, Contains.Item(new NodaTime.LocalDate(2025, 02, 24)));
                Assert.That(output, Contains.Item(new NodaTime.LocalDate(2025, 03, 03)));
                Assert.That(output, Contains.Item(new NodaTime.LocalDate(2025, 03, 10)));
                Assert.That(output, Contains.Item(new NodaTime.LocalDate(2025, 03, 17)));
                Assert.That(output, Contains.Item(new NodaTime.LocalDate(2025, 03, 24)));
                Assert.That(output, Contains.Item(new NodaTime.LocalDate(2025, 03, 31)));
                Assert.That(output, Contains.Item(new NodaTime.LocalDate(2025, 04, 07)));
                Assert.That(output, Contains.Item(new NodaTime.LocalDate(2025, 04, 14)));
                Assert.That(output, Contains.Item(new NodaTime.LocalDate(2025, 04, 21)));
                Assert.That(output, Contains.Item(new NodaTime.LocalDate(2025, 04, 28)));
                Assert.That(output, Contains.Item(new NodaTime.LocalDate(2025, 05, 05)));
            });
        }

        [Test]
        public void ParseDateTest4()
        {
            string input = "";
            var output = _reader.ParseSchedule("15.02-15.03 ч.н., 22.03-19.04 к.н.", 2025);

            Assert.Multiple(() =>
            {
                Assert.That(output.Count(), Is.EqualTo(8));
                Assert.That(output, Contains.Item(new NodaTime.LocalDate(2025, 02, 15)));
                Assert.That(output, Contains.Item(new NodaTime.LocalDate(2025, 03, 01)));
                Assert.That(output, Contains.Item(new NodaTime.LocalDate(2025, 03, 15)));
                Assert.That(output, Contains.Item(new NodaTime.LocalDate(2025, 03, 22)));
                Assert.That(output, Contains.Item(new NodaTime.LocalDate(2025, 03, 29)));
                Assert.That(output, Contains.Item(new NodaTime.LocalDate(2025, 04, 05)));
                Assert.That(output, Contains.Item(new NodaTime.LocalDate(2025, 04, 12)));
                Assert.That(output, Contains.Item(new NodaTime.LocalDate(2025, 04, 19)));
            });
        }

        [Test]
        public void ParseWithDotTest()
        {
            string input = "Технологии индустрии 4.0. Волкова О.Р. семинар. 409. [19.02-12.03 к.н.] Технологии индустрии 4.0. Волкова О.Р. семинар. 308. [19.03-07.05 к.н.]";

            var output = _reader.ParseLessons(input, _time, _period, _group);

            Assert.Multiple(() =>
            {
                Assert.That(output[0].Subject, Is.EqualTo("Технологии индустрии 4.0"));
                Assert.That(output[0].Teacher, Is.EqualTo("Волкова О.Р."));
                Assert.That(output[0].Type, Is.EqualTo("семинар"));
                Assert.That(output[0].Cabinet, Is.EqualTo("409"));
                Assert.That(string.IsNullOrEmpty(output[0].Subgroup));

                Assert.That(output[1].Subject, Is.EqualTo("Технологии индустрии 4.0"));
                Assert.That(output[1].Teacher, Is.EqualTo("Волкова О.Р."));
                Assert.That(output[1].Type, Is.EqualTo("семинар"));
                Assert.That(output[1].Cabinet, Is.EqualTo("308"));
                Assert.That(string.IsNullOrEmpty(output[1].Subgroup));
            });
        }

        [Test]
        public void ParseWithDashTest()
        {
            /* 
             * Иногда после '-' ставится пробел. Он должен игнорироваться
             */
            string input = "Графические системы и интерфейс оператора. Евстафиева С.В. семинар. 355. [17.02-10.03 к.н.] Объектно- ориентированное проектирование. Евстафиева С.В. семинар. 355. [17.03-21.04 к.н.]";

            var output = _reader.ParseLessons(input, _time, _period, _group);

            Assert.Multiple(() =>
            {
                Assert.That(output[0].Subject, Is.EqualTo("Графические системы и интерфейс оператора"));
                Assert.That(output[0].Teacher, Is.EqualTo("Евстафиева С.В."));
                Assert.That(output[0].Type, Is.EqualTo("семинар"));
                Assert.That(output[0].Cabinet, Is.EqualTo("355"));
                Assert.That(string.IsNullOrEmpty(output[0].Subgroup));

                Assert.That(output[1].Subject, Is.EqualTo("Объектно-ориентированное проектирование"));
                Assert.That(output[1].Teacher, Is.EqualTo("Евстафиева С.В."));
                Assert.That(output[1].Type, Is.EqualTo("семинар"));
                Assert.That(output[1].Cabinet, Is.EqualTo("355"));
                Assert.That(string.IsNullOrEmpty(output[1].Subgroup));
            });
        }

        [Test]
        public void ParseTest()
        {
            string input = "Основы проектирования и разработки Web-приложений. Иванов И.И. лабораторные занятия. (Б). 235(и). [19.03-30.04 ч.н.]";

            var output = _reader.ParseLessons(input, _time, _period, _group);

            Assert.Multiple(() =>
            {
                Assert.That(output[0].Subject, Is.EqualTo("Основы проектирования и разработки Web-приложений"));
                Assert.That(output[0].Teacher, Is.EqualTo("Иванов И.И."));
                Assert.That(output[0].Type, Is.EqualTo("лабораторные занятия"));
                Assert.That(output[0].Cabinet, Is.EqualTo("235(и)"));
                Assert.That(output[0].Subgroup, Is.EqualTo("Б"));
            });
        }

        [Test]
        public void ParseWithSymbolTest()
        {
            string input = "Введение в проектную деятельность: технологическое и социальное проектирование. Окоракова А.А. лекции. 0801. [12.02-19.03 к.н., 02.04, 09.04] Психология и педагогика. Кузнецов Б.М. семинар. 504. [23.04-28.05 к.н.]";

            var output = _reader.ParseLessons(input, _time, _period, _group);

            Assert.Multiple(() =>
            {
                Assert.That(output[0].Subject, Is.EqualTo("Введение в проектную деятельность: технологическое и социальное проектирование"));
                Assert.That(output[0].Teacher, Is.EqualTo("Окоракова А.А."));
                Assert.That(output[0].Type, Is.EqualTo("лекции"));
                Assert.That(output[0].Cabinet, Is.EqualTo("0801"));
            });
        }
    }
}