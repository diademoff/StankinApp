1. [python скрипт](./pdfparser/main.py) парсит pdf в json. Одна группа - один json.
2. [c# database](./StankinAppDatabase/StankinAppDatabase/) позволяет создавать базу данных из json. Создаётся sql база для всего заведения.
3. [c# web api](./StankinAppDatabase/StankinAppApi/). предоставляет доступ к БД.
4. [alpine js + tailwind](./Web/) pwa frontend