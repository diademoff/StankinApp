# StankinApp — Расписание МГТУ Станкин

PWA-приложение для просмотра расписания занятий [ФГАОУ ВО МГТУ «Станкин»](https://stankin.ru).
Расписание кэшируется на устройстве и доступно **offline**.

---

## Архитектура

Репозиторий — монорепо из четырёх независимых компонентов:

```
.
├── pdfparser/              # Python: парсит PDF расписания → JSON
├── StankinAppDatabase/
│   ├── StankinAppCore/     # C# библиотека: модели, чтение БД
│   ├── StankinAppDatabase/ # C# CLI: строит SQLite из JSON
│   ├── StankinAppApi/      # C# Web API: REST поверх SQLite
│   └── StankinAppDatabase.Tests/
├── stankin-schedule/       # TypeScript PWA: Alpine.js + Tailwind
└── deploy/                 # Nginx, Docker Compose, сертификаты
```

### Поток данных

```
PDF расписания (опубликованы на сайте университета)
    │
    ▼
pdfparser/main.py   →  JSON файлы (одна группа = один файл)
    │
    ▼
StankinAppDatabase  →  schedule.db  (SQLite, всё расписание)
    │
    ▼
StankinAppApi       →  REST JSON API  (GET /api/schedule, /api/groups, ...)
    │
    ▼
stankin-schedule    →  PWA в браузере
```

---

## Требования

| Компонент         | Инструмент |
|-------------------|------------------------|
| PDF парсер        | Python 3.10+           |
| База данных / API | .NET 8 SDK             |
| Фронтенд          | Node.js 20+, npm       |
| Деплой            | Docker, Docker Compose |

---

## Быстрый старт (разработка)

### 1. Парсинг PDF в JSON

```bash
cd pdfparser
./install_venv.sh          # создаёт venv и ставит зависимости
source .venv/bin/activate
python main.py             # укажите путь к PDF по запросу скрипта
```

Результат: папка с JSON-файлами по группам.

### 2. Сборка базы данных

```bash
cd StankinAppDatabase/StankinAppDatabase
dotnet run -- create --json-path /path/to/json-folder --year 2026 --db-save-path schedule.db
```

[Что делать при ошибке парсинга.](./StankinAppDatabase/StankinAppDatabase/README.md)

Файл `schedule.db` — готовая SQLite база, которую нужно положить в `./deploy/data/`.

### 3. Запуск API

```bash
cd StankinAppDatabase/StankinAppApi
dotnet run
# API слушает http://localhost:5000
```

Проверить:
```
GET http://localhost:5000/api/groups
GET http://localhost:5000/api/schedule?groupName=АДБ-23-04&startDate=2026-03-29&endDate=2026-04-04
```

Коллекция запросов Bruno: `StankinAppDatabase/StankinAppApi/api/`

### 4. Запуск фронтенда

```bash
cd stankin-schedule
npm install
npm run dev
# Открыть http://localhost:5173
```

---

## Конфигурация

### База данных

Путь к SQLite в `StankinAppDatabase/StankinAppApi/appsettings.json`:

```json
{
  "Database": {
    "Path": "data/stankin.db"
  }
}
```

Для разработки — `appsettings.Development.json`:

```json
{
  "Database": {
    "Path": "schedule.db"
  }
}
```

---

## Тестирование API вручную (Bruno)

В папке `StankinAppDatabase/StankinAppApi/api/` лежит коллекция [Bruno](https://www.usebruno.com/).

---

## Отладка на мобильном устройстве (локальная сеть)

```bash
# Узнать IP машины
ip addr show | grep inet

# Запустить фронтенд доступным в локальной сети
cd stankin-schedule
npm run dev -- --host

# Запустить API
cd StankinAppDatabase/StankinAppApi
dotnet run --urls "http://0.0.0.0:5000"

# Открыть файрвол при необходимости
sudo ufw allow 5173/tcp
sudo ufw allow 5000/tcp
```

Затем с телефона открыть `http://<IP машины>:5173`.

Для Chrome DevTools remote debugging:
```bash
chromium --remote-debugging-port=9222
```

---

## Запуск тестов

```bash
cd StankinAppDatabase
dotnet test StankinAppDatabase.Tests/
```

---

## Деплой (Docker Compose)

### Структура

```
deploy/
├── data/           # Сюда кладём stankin.db
├── nginx/
│   ├── conf.d/     # Конфиг виртуального хоста
│   ├── nginx.conf
│   └── proxy_params
docker-compose.yml
```


### Первичный деплой на чистый VPS (Ubuntu)

#### 1. Подготовка сервера

```bash
# Установить Docker (https://docs.docker.com/engine/install/ubuntu/)
apt install git docker.io

# Открыть порты
ufw allow 80
ufw allow 443
```

#### 2. Клонировать репо и подготовить структуру

```bash
git clone <repo-url> && cd StankinApp
mkdir -p deploy/nginx/conf.d deploy/certbot/www deploy/certbot/conf data
```

#### 3. Скопировать базу данных

```bash
scp /local/path/stankin.db stankin@<server-ip>:/home/stankin/StankinApp/deploy/data/
```

#### 4. Получить SSL-сертификат (Let's Encrypt)

```bash
# Шаг 1: временно закомментировать HTTPS-блок в deploy/nginx/conf.d/default.conf
# Шаг 2: поднять только Nginx
docker compose up -d web

# Шаг 3: получить сертификат через webroot
docker run --rm -it \
  -v "/etc/letsencrypt:/etc/letsencrypt" \
  -v "$(pwd)/deploy/nginx/html:/var/www/certbot" \
  certbot/certbot certonly --webroot \
  -w /var/www/certbot -d stankinapp.ru \
  --email your@email.ru --agree-tos

# Шаг 4: вернуть HTTPS-блок в nginx конфиг
# Шаг 5: пробросить сертификаты
sudo ln -sf /etc/letsencrypt ./deploy/certbot/conf
```

#### 5. Автопродление сертификата

```bash
crontab -e
# Добавить строку:
0 3 * * * docker run --rm \
  -v "/etc/letsencrypt:/etc/letsencrypt" \
  -v "/home/stankin/StankinApp/deploy/nginx/html:/var/www/certbot" \
  certbot/certbot renew --quiet && \
  docker exec stankinapp_web_1 nginx -s reload
```

#### 6. Запуск

```bash
./deploy.sh
docker compose up -d --build
```

---

## Структура API

| Метод | Эндпоинт                 | Описание                             |
|-------|--------------------------|--------------------------------------|
| GET   | `/api/groups`            | Список всех учебных групп            |
| GET   | `/api/rooms`             | Список аудиторий                     |
| GET   | `/api/teachers`          | Список преподавателей                |
| GET   | `/api/schedule`          | Расписание группы за период          |
| GET   | `/api/schedule/teacher`  | Расписание преподавателя за период   |
| GET   | `/api/teachers/validate` | Проверка существования преподавателя |

**Параметры `/api/schedule` (Группы):**

| Параметр | Тип | Пример |
|----------|-----|--------|
| `groupName` | string | `АДБ-23-04` |
| `startDate` | string (ISO) | `2026-03-29` |
| `endDate` | string (ISO) | `2026-04-04` |

**Параметры `/api/schedule/teacher` (Преподаватели):**

| Параметр | Тип | Пример |
|----------|-----|--------|
| `teacherName` | string | `Иванов И.И.` |
| `startDate` | string (ISO) | `2026-03-29` |
| `endDate` | string (ISO) | `2026-04-04` |

**Формат ответа (единый для всех эндпоинтов расписания):**

```json
{
  "metadata": {
    "nextWeek": "2026-04-05",
    "prevWeek": "2026-03-22",
    "periodStart": "2026-03-29",
    "periodEnd": "2026-04-04",
    "isLastWeek": false
  },
  "items": [
    {
      "id": "АДБ-23-04_2026-03-29_08:30_all",
      "date": "2026-03-29",
      "startTime": "08:30",
      "endTime": "10:00",
      "durationMinutes": 90,
      "subject": "Математика",
      "teacher": "Иванов И.И.",
      "groupName": "АДБ-23-04",
      "type": "Лекция",
      "cabinet": "А-101",
      "subgroup": "",
      "sequencePosition": 1,
      "sequenceLength": 1
    }
  ]
}
```

HTTP-статусы: `200` — данные есть, `204` — пар за период нет, `400` — неверные параметры.

---

## Стек технологий

| Слой | Технология | Лицензия |
|------|-----------|---------|
| PDF парсер | Python | MIT |
| БД / Core | .NET 8, SQLite | MIT / Public Domain |
| Web API | ASP.NET Core, Dapper, NodaTime | MIT |
| Мониторинг | OpenTelemetry, Serilog | Apache 2.0 |
| Фронтенд | Alpine.js, Tailwind CSS, Vite | MIT |
| PWA | vite-plugin-pwa, Workbox | MIT |
| Деплой | Docker, Nginx | Apache 2.0 / BSD |
| API-тесты | Bruno | MIT |
