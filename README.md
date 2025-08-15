1. [python скрипт](./pdfparser/main.py) парсит pdf в json. Одна группа - один json.
2. [c# database](./StankinAppDatabase/StankinAppDatabase/) позволяет создавать базу данных из json. Создаётся sql база для всего заведения.
3. [c# web api](./StankinAppDatabase/StankinAppApi/). предоставляет доступ к БД.
4. [alpine js + tailwind](./Web/) pwa frontend

For live server
```
"liveServer.settings.ignoreFiles": [
  ".vscode/**",
  "**/*.scss",
  "**/*.sass",
  "**/*.ts",
  "StankinAppDatabase/**"
]
```

Run
```sh
sudo npm install -g live-server
sudo ufw allow 5173/tcp
sudo ufw allow 5001/tcp

# ip addr: 192.168.... -> тут сайт 192.168.0.103
# + В `config:js` вставить ip.
ip addr show  | grep inet

cd .../StankinApp/Web/
live-server --host=0.0.0.0 --port=5173

dotnet run --urls "http://0.0.0.0:5001"
```

В api:

```cs
app.Urls.Add("http://192.168.0.103:5001");
app.Urls.Add("https://192.168.0.103:5002");
```