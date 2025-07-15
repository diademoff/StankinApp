#!/bin/bash

fallback() {
    echo "Звершено с ошибкой"
    exit 1
}

cd pdfparser/

if [ -d "./pdf/" ]; then
    echo "Папка pdf найдена"
else
    echo "Папка pdfparser/pdf/ не существует"
    fallback
fi

if [ -d "./.venv" ]; then
    echo "Папка .venv найдена"
    source .venv/bin/activate
else
    echo "Папка .venv не существует"
    echo "Запуск install_venv.sh"
    ./install_venv.sh
    if [ $? -eq 0 ]; then
        echo "Скрипт install_venv успешно завершён"
    else
        echo "Ошибка: Скрипт install_venv завершился с кодом $?"
        fallback
    fi
fi

mkdir json
cd json

python ../main.py

cd ../../StankinAppDatabase/StankinAppDatabase

echo "Сборка dbBuilder"

dotnet publish --output ../../dbBuilder

cd ../../dbBuilder

./StankinAppDatabase --path ../pdfparser/json/ --year 2025