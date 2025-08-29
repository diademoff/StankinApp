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

IP адреса хранятся в:
- `StankinAppDatabase/StankinAppApi/Program.cs`
- `StankinAppDatabase/StankinAppApi/StartupExtensions.cs`
- `Web/src/config.js`

Debug
```
chromium --remote-debugging-port=9222
```

Run
```sh
npm install
npm run dev
```

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


Deploy
```sh
apt install docker docker-compose git npm nginx certbot python3-certbot-nginx
# StartupExtensions.cs set available ip
# prometheus.yml и добавить API как таргет targets: ['192.168.0.103:5001']
./prometheus --config.file=prometheus.yml

# в корне репы
mkdir -p data deploy/nginx/conf.d deploy/certbot/www deploy/certbot/conf
# скопируйте sqlite в ./data/stankin.db
scp /home/dmff/repos/StankinApp/dbBuilder/schedule.db stankin@89.111.131.170:/home/stankin/stankin.db


# проверить путь к ssl файлам
docker compose build --pull
docker compose up -d
```
<!-- TODO -->
Building api
DEPRECATED: The legacy builder is deprecated and will be removed in a future release.
            Install the buildx component to build images with BuildKit:
            https://docs.docker.com/go/buildx/

### info
```sh
docker ps           # только запущенные
docker ps -a        # все, включая остановленные
docker start <container_id_or_name>    # запустить остановленный
docker stop <container_id_or_name>     # остановить
docker restart <container_id_or_name>  # перезапустить
docker rm <container_id_or_name>       # удалить контейнер
docker logs -f <container_id_or_name>   # непрерывный вывод
docker exec -it <container_id_or_name> /bin/sh   # или /bin/bash
docker compose up -d       # запуск всех сервисов в фоне
docker compose down        # остановка и удаление контейнеров
docker compose ps          # статус контейнеров
docker compose logs -f     # вывод логов всех сервисов
docker compose exec <service> /bin/sh  # попасть внутрь контейнера сервиса

docker compose logs -f api
docker compose logs -f web

docker compose exec api curl -s http://localhost:5000/metrics | head
docker-compose run --rm api sh
```

```md
user: stankin:stankinapp
su - stankin

<!-- Получить сертификат -->
sudo mkdir -p /etc/letsencrypt
sudo certbot certonly --nginx -d stankinapp.ru --email diademoff@yandex.ru --agree-tos --non-interactive
<!-- В корне репы -->
sudo ln -sf /etc/letsencrypt ./deploy/certbot/conf || sudo rm -rf ./deploy/certbot/conf && sudo ln -sf /etc/letsencrypt ./deploy/certbot/conf

<!-- Потом: Раскомментируйте HTTPS в Nginx и перезагрузит -->
sudo certbot renew --dry-run
```

clean docker: `docker system prune -a -f`