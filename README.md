Веб-приложение для просмотра расписания занятий в университете ФГАОУ ВО МГТУ Станкин.


1. [python скрипт](./pdfparser/main.py) парсит pdf в json. Одна группа - один json.
2. [c# database](./StankinAppDatabase/StankinAppDatabase/) позволяет создавать базу данных из json. Создаётся sql база для всего университета целиком.
3. [c# web api](./StankinAppDatabase/StankinAppApi/). предоставляет доступ к БД.
4. [alpine js + tailwind](./stankin-schedule/) pwa frontend

# Dev

Для разработки в visual studio code с расширением `live server` установите настройки:
```
"liveServer.settings.ignoreFiles": [
  ".vscode/**",
  "**/*.scss",
  "**/*.sass",
  "**/*.ts",
  "StankinAppDatabase/**"
]
```

На случай, если необходимо перенести сайт на другой сервер/домен. IP адреса хранятся в:
- `StankinAppDatabase/StankinAppApi/Program.cs`
- `StankinAppDatabase/StankinAppApi/StartupExtensions.cs`
- `stankin-schedule/src/config.js`


Для отладки можно использовать
```
chromium --remote-debugging-port=9222
```

Запуск front end (в папке `./stankin-schedule`)
```sh
npm install
npm run dev
```

Для debug запуска, используя live-server, чтобы протестировать на устройствах в локальной сети (например, зайти с телефона)
```sh
sudo npm install -g live-server
sudo ufw allow 5173/tcp
sudo ufw allow 5001/tcp

# ip addr -> тут сайт 192.168.0.103
# + В `config:js` вставить ip.
ip addr show  | grep inet

# В папке stankin-schedule
live-server --host=0.0.0.0 --port=5173

# В папке StankinAppDatabase
dotnet run --urls "http://0.0.0.0:5001"
```

Для изменения порта api (`./StankinAppDatabase/StankinAppApi/Program.cs`):

```cs
app.Urls.Add("http://192.168.0.103:5001");
app.Urls.Add("https://192.168.0.103:5002");
```


# Deploy

Deploy происходит через docker
```sh
docker compose up -d --build # запуск всех сервисов в фоне
docker compose down          # остановка и удаление контейнеров
docker compose ps            # статус контейнеров
docker system prune -a -f    # очистить всё

docker compose logs -f api
docker compose logs -f web
```

На чистом VPS Ubuntu server выполняем следующее
```sh
user: stankin:stankinapp
su - stankin

# Получить сертификат
sudo mkdir -p /etc/letsencrypt
sudo certbot certonly --nginx -d stankinapp.ru --email diademoff@yandex.ru --agree-tos --non-interactive
# В корне репы
sudo ln -sf /etc/letsencrypt ./deploy/certbot/conf || sudo rm -rf ./deploy/certbot/conf && sudo ln -sf /etc/letsencrypt ./deploy/certbot/conf



# Потом: Раскомментируйте HTTPS в Nginx и перезагрузит
sudo certbot renew --dry-run
```

```sh
# Скачать docker и 'docker compose' https://docs.docker.com/engine/install/ubuntu/
apt install git

ufw allow 80

# в корне репы
mkdir -p data deploy/nginx/conf.d deploy/certbot/www deploy/certbot/conf
# скопируйте sqlite в ./data/stankin.db
scp /path/to/database/schedule.db stankin@89.111.131.170:/path/to/deploy/data/put-stankindb-here

# Первый раз получить ssl сертификат
# Вам нужно временно «отключить» HTTPS-секцию, чтобы Nginx смог стартовать только на 80-м порту.
# deploy/nginx/conf.d/default.conf

# Теперь запустите только веб-сервер
docker compose up -d web

# Получить сертификат в первый раз
docker run --rm -it --name certbot -v "/etc/letsencrypt:/etc/letsencrypt" -v "$(pwd)/deploy/nginx/html:/var/www/certbot" certbot/certbot certonly --webroot -w /var/www/certbot -d stankinapp.ru --email diademoff@yandex.ru --agree-tos

# Автопродление сертификата (проверьте абсолютный путь до StankinApp)
crontab -e
0 3 * * * docker run --rm -v "/etc/letsencrypt:/etc/letsencrypt" -v "/home/stankin/StankinApp/deploy/nginx/html:/var/www/certbot" certbot/certbot renew --quiet && docker exec stankinapp_web_1 nginx -s reload

# Продлить сертификат вручную (без --dry-run)
docker run --rm -v "/etc/letsencrypt:/etc/letsencrypt" -v "$(pwd)/deploy/nginx/html:/var/www/certbot" certbot/certbot renew --dry-run

# Возвращаем https в nginx: deploy/nginx/conf.d/default.conf

# Установите значения в .env файле

# Деплой (не забудь обновить год)
./deploy.sh

# Не забудь проверить путь к ssl файлам
docker compose up -d --build
```