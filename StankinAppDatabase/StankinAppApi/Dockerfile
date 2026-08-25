# build self-contained .NET app
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# копируем общую библиотеку и проект api
COPY ./StankinAppDatabase/StankinAppCore ./StankinAppCore
COPY ./StankinAppDatabase/StankinAppApi/*.csproj ./StankinAppApi/
RUN dotnet restore ./StankinAppApi/*.csproj

# копируем остальное и публикуем для linux-x64 (измените RID при необходимости)
COPY . .
RUN dotnet publish ./StankinAppDatabase/StankinAppApi/*.csproj \
    -c Release -r linux-x64 --self-contained true -o /app/publish /p:PublishTrimmed=false

# runtime image
FROM mcr.microsoft.com/dotnet/runtime-deps:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Создаём директорию для БД (чтобы volume mount работал)
RUN mkdir -p /app/data

ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000

# контейнер запускается от root — доска пишет board.db в бинд-маунт deploy/data,
# чьи права владения на сервере не гарантируются для uid 1000 (иначе SQLite Error 14)
ENTRYPOINT ["./StankinAppApi"]