# Инструкция по добавлению базы данных в ресурсы приложения

## Шаги для добавления файла schedule.db:

1. Создайте папку `assets` в директории `app/src/main/`, если её нет:
   - Правый клик на папку `main`
   - New -> Folder -> Assets Folder

2. Скопируйте файл `schedule.db` в созданную папку `assets`

3. В коде приложения добавьте метод для копирования базы данных:

```kotlin
private fun copyDatabaseFromAssets() {
    val dbPath = context.getDatabasePath("schedule.db").absolutePath
    
    if (!File(dbPath).exists()) {
        try {
            val inputStream = context.assets.open("schedule.db")
            val outputStream = FileOutputStream(dbPath)
            
            inputStream.copyTo(outputStream)
            inputStream.close()
            outputStream.close()
        } catch (e: IOException) {
            e.printStackTrace()
        }
    }
}
```

4. Вызовите этот метод при инициализации базы данных:

```kotlin
class DatabaseBuilder(private val context: Context) {
    init {
        copyDatabaseFromAssets()
    }
    // ... остальной код
}
```

## Важные замечания:

- Файл базы данных должен быть предварительно создан и заполнен данными
- Размер файла базы данных влияет на размер APK
- База данных копируется только при первом запуске приложения
- Убедитесь, что у приложения есть права на запись во внутреннее хранилище

## Проверка:

После добавления файла и кода:
1. Соберите приложение
2. Запустите его
3. Проверьте, что база данных скопировалась в:
   ```
   /data/data/com.dmff.stankinapp/databases/schedule.db
   ``` 