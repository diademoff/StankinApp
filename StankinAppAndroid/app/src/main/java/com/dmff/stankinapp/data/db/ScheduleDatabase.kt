package com.dmff.stankinapp.data.db

import android.content.Context
import android.database.sqlite.SQLiteDatabase
import android.database.sqlite.SQLiteOpenHelper
import android.util.Log
import android.widget.Toast
import com.dmff.stankinapp.data.model.Course
import com.dmff.stankinapp.data.model.Schedule
import java.io.File
import java.time.LocalDate
import java.time.LocalTime
import java.time.format.DateTimeFormatter

class ScheduleDatabase(context: Context) : SQLiteOpenHelper(context, "schedule.db", null, 1) {
    private val appContext = context.applicationContext

    companion object {
        private const val DATABASE_VERSION = 1
        private const val DATABASE_NAME = "schedule.db"

        // Table names
        private const val TABLE_GROUPS = "groups"
        private const val TABLE_TEACHERS = "teachers"
        private const val TABLE_ROOMS = "rooms"
        private const val TABLE_SESSIONS = "sessions"
        private const val TABLE_LESSONS = "lessons"
        private const val TABLE_SCHEDULE_DATES = "schedule_dates"

        // Column names
        private const val COLUMN_ID = "id"
        private const val COLUMN_NAME = "name"
        private const val COLUMN_GROUP_ID = "group_id"
        private const val COLUMN_START_TIME = "start_time"
        private const val COLUMN_END_TIME = "end_time"
        private const val COLUMN_SESSION_ID = "session_id"
        private const val COLUMN_SUBJECT = "subject"
        private const val COLUMN_TEACHER_ID = "teacher_id"
        private const val COLUMN_LESSON_TYPE = "lesson_type"
        private const val COLUMN_ROOM_ID = "room_id"
        private const val COLUMN_SUBGROUP = "subgroup"
        private const val COLUMN_LESSON_ID = "lesson_id"
        private const val COLUMN_DATE = "date"
    }

    init {
        try {
            checkDatabaseConnection()
        } catch (e: Exception) {
            Log.e("ScheduleDatabase", "Database error: ${e.message}", e)
            Toast.makeText(
                appContext,
                "Ошибка подключения к базе данных: ${e.message}",
                Toast.LENGTH_LONG
            ).show()
        }
    }

    override fun onCreate(db: SQLiteDatabase) {
        createTables(db)
    }

    override fun onUpgrade(db: SQLiteDatabase, oldVersion: Int, newVersion: Int) {
        // No migrations needed
    }

    private fun createTables(db: SQLiteDatabase) {
        // Create groups table
        db.execSQL("""
            CREATE TABLE IF NOT EXISTS $TABLE_GROUPS (
                $COLUMN_ID INTEGER PRIMARY KEY AUTOINCREMENT,
                $COLUMN_NAME TEXT NOT NULL UNIQUE
            )
        """)

        // Create teachers table
        db.execSQL("""
            CREATE TABLE IF NOT EXISTS $TABLE_TEACHERS (
                $COLUMN_ID INTEGER PRIMARY KEY AUTOINCREMENT,
                $COLUMN_NAME TEXT NOT NULL UNIQUE
            )
        """)

        // Create rooms table
        db.execSQL("""
            CREATE TABLE IF NOT EXISTS $TABLE_ROOMS (
                $COLUMN_ID INTEGER PRIMARY KEY AUTOINCREMENT,
                $COLUMN_NAME TEXT NOT NULL UNIQUE
            )
        """)

        // Create sessions table
        db.execSQL("""
            CREATE TABLE IF NOT EXISTS $TABLE_SESSIONS (
                $COLUMN_ID INTEGER PRIMARY KEY AUTOINCREMENT,
                $COLUMN_GROUP_ID INTEGER NOT NULL,
                $COLUMN_START_TIME TEXT NOT NULL,
                $COLUMN_END_TIME TEXT NOT NULL,
                FOREIGN KEY ($COLUMN_GROUP_ID) REFERENCES $TABLE_GROUPS($COLUMN_ID)
            )
        """)

        // Create lessons table
        db.execSQL("""
            CREATE TABLE IF NOT EXISTS $TABLE_LESSONS (
                $COLUMN_ID INTEGER PRIMARY KEY AUTOINCREMENT,
                $COLUMN_SESSION_ID INTEGER NOT NULL,
                $COLUMN_SUBJECT TEXT NOT NULL,
                $COLUMN_TEACHER_ID INTEGER NOT NULL,
                $COLUMN_LESSON_TYPE TEXT NOT NULL,
                $COLUMN_ROOM_ID INTEGER,
                $COLUMN_SUBGROUP TEXT,
                FOREIGN KEY ($COLUMN_SESSION_ID) REFERENCES $TABLE_SESSIONS($COLUMN_ID),
                FOREIGN KEY ($COLUMN_TEACHER_ID) REFERENCES $TABLE_TEACHERS($COLUMN_ID),
                FOREIGN KEY ($COLUMN_ROOM_ID) REFERENCES $TABLE_ROOMS($COLUMN_ID)
            )
        """)

        // Create schedule_dates table
        db.execSQL("""
            CREATE TABLE IF NOT EXISTS $TABLE_SCHEDULE_DATES (
                $COLUMN_ID INTEGER PRIMARY KEY AUTOINCREMENT,
                $COLUMN_LESSON_ID INTEGER NOT NULL,
                $COLUMN_DATE TEXT NOT NULL,
                FOREIGN KEY ($COLUMN_LESSON_ID) REFERENCES $TABLE_LESSONS($COLUMN_ID)
            )
        """)
    }

    private fun checkDatabaseConnection() {
        val dbFile = appContext.getDatabasePath(DATABASE_NAME)
        if (!dbFile.exists()) {
            Log.e("ScheduleDatabase", "Database file not found at ${dbFile.absolutePath}")
            throw IllegalStateException("База данных не найдена")
        }

        val fileSize = dbFile.length()
        Log.d("ScheduleDatabase", "Database file size: $fileSize bytes")

        val db = readableDatabase
        val version = db.version
        Log.d("ScheduleDatabase", "Database version: $version")

        // Check if tables exist and have data
        val tables = listOf(TABLE_GROUPS, TABLE_TEACHERS, TABLE_ROOMS, TABLE_SESSIONS, TABLE_LESSONS, TABLE_SCHEDULE_DATES)
        for (table in tables) {
            val cursor = db.rawQuery("SELECT COUNT(*) FROM $table", null)
            cursor.moveToFirst()
            val count = cursor.getInt(0)
            cursor.close()
            Log.d("ScheduleDatabase", "Table $table count: $count")
            if (count == 0 && table == TABLE_GROUPS) {
                throw IllegalStateException("База данных пуста")
            }
        }
    }

    fun processSchedule(schedule: Schedule) {
        val db = writableDatabase
        db.beginTransaction()
        try {
            // Insert or get group ID
            val groupId = getOrCreate(db, TABLE_GROUPS, COLUMN_NAME, schedule.groupName)

            // Process teachers and rooms
            val teachers = schedule.days.mapNotNull { it.teacher }.distinct()
            val rooms = schedule.days.mapNotNull { it.cabinet }.distinct()

            val teacherIds = teachers.associateWith { teacher ->
                getOrCreate(db, TABLE_TEACHERS, COLUMN_NAME, teacher)
            }

            val roomIds = rooms.associateWith { room ->
                getOrCreate(db, TABLE_ROOMS, COLUMN_NAME, room)
            }

            // Process each course
            for (course in schedule.days) {
                // Insert session
                val sessionId = insertSession(db, groupId, course)

                // Insert lesson
                val teacherId = teacherIds[course.teacher] ?: continue
                val roomId = course.cabinet?.let { roomIds[it] }

                val lessonId = insertLesson(db, sessionId, course, teacherId, roomId)

                // Insert schedule dates
                course.dates?.forEach { date ->
                    insertScheduleDate(db, lessonId, date)
                }
            }

            db.setTransactionSuccessful()
        } finally {
            db.endTransaction()
        }
    }

    private fun getOrCreate(db: SQLiteDatabase, table: String, column: String, value: String): Long {
        val cursor = db.query(
            table,
            arrayOf(COLUMN_ID),
            "$column = ?",
            arrayOf(value),
            null,
            null,
            null
        )

        return if (cursor.moveToFirst()) {
            val id = cursor.getLong(0)
            cursor.close()
            id
        } else {
            cursor.close()
            val values = android.content.ContentValues().apply {
                put(column, value)
            }
            db.insert(table, null, values)
        }
    }

    private fun insertSession(db: SQLiteDatabase, groupId: Long, course: Course): Long {
        val values = android.content.ContentValues().apply {
            put(COLUMN_GROUP_ID, groupId)
            put(COLUMN_START_TIME, course.startTime.format(DateTimeFormatter.ofPattern("HH:mm")))
            put(COLUMN_END_TIME, course.startTime.plus(course.duration!!)
                .format(DateTimeFormatter.ofPattern("HH:mm")))
        }
        return db.insert(TABLE_SESSIONS, null, values)
    }

    private fun insertLesson(
        db: SQLiteDatabase,
        sessionId: Long,
        course: Course,
        teacherId: Long,
        roomId: Long?
    ): Long {
        val values = android.content.ContentValues().apply {
            put(COLUMN_SESSION_ID, sessionId)
            put(COLUMN_SUBJECT, course.subject ?: "")
            put(COLUMN_TEACHER_ID, teacherId)
            put(COLUMN_LESSON_TYPE, course.type ?: "")
            put(COLUMN_ROOM_ID, roomId)
            put(COLUMN_SUBGROUP, course.subgroup)
        }
        return db.insert(TABLE_LESSONS, null, values)
    }

    private fun insertScheduleDate(db: SQLiteDatabase, lessonId: Long, date: LocalDate) {
        val values = android.content.ContentValues().apply {
            put(COLUMN_LESSON_ID, lessonId)
            put(COLUMN_DATE, date.format(DateTimeFormatter.ofPattern("dd.MM")))
        }
        db.insert(TABLE_SCHEDULE_DATES, null, values)
    }

    fun getGroups(): List<String> {
        val groups = mutableListOf<String>()
        val db = readableDatabase
        val cursor = db.query(
            TABLE_GROUPS,
            arrayOf(COLUMN_NAME),
            null,
            null,
            null,
            null,
            COLUMN_NAME
        )

        while (cursor.moveToNext()) {
            groups.add(cursor.getString(0))
        }
        cursor.close()
        return groups
    }

    fun getRooms(): List<String> {
        val rooms = mutableListOf<String>()
        val db = readableDatabase
        val cursor = db.query(
            TABLE_ROOMS,
            arrayOf(COLUMN_NAME),
            null,
            null,
            null,
            null,
            COLUMN_NAME
        )

        while (cursor.moveToNext()) {
            rooms.add(cursor.getString(0))
        }
        cursor.close()
        return rooms
    }

    fun getTeachers(): List<String> {
        val teachers = mutableListOf<String>()
        val db = readableDatabase
        val cursor = db.query(
            TABLE_TEACHERS,
            arrayOf(COLUMN_NAME),
            null,
            null,
            null,
            null,
            COLUMN_NAME
        )

        while (cursor.moveToNext()) {
            teachers.add(cursor.getString(0))
        }
        cursor.close()
        return teachers
    }

    fun getScheduleForGroup(groupName: String, date: LocalDate): List<Course> {
        val courses = mutableListOf<Course>()
        val db = readableDatabase
        val query = """
            SELECT l.$COLUMN_SUBJECT, t.$COLUMN_NAME, l.$COLUMN_LESSON_TYPE, r.$COLUMN_NAME,
                   s.$COLUMN_START_TIME, s.$COLUMN_END_TIME, l.$COLUMN_SUBGROUP, g.$COLUMN_NAME
            FROM $TABLE_LESSONS l
            JOIN $TABLE_SESSIONS s ON l.$COLUMN_SESSION_ID = s.$COLUMN_ID
            JOIN $TABLE_GROUPS g ON s.$COLUMN_GROUP_ID = g.$COLUMN_ID
            JOIN $TABLE_TEACHERS t ON l.$COLUMN_TEACHER_ID = t.$COLUMN_ID
            LEFT JOIN $TABLE_ROOMS r ON l.$COLUMN_ROOM_ID = r.$COLUMN_ID
            JOIN $TABLE_SCHEDULE_DATES sd ON l.$COLUMN_ID = sd.$COLUMN_LESSON_ID
            WHERE g.$COLUMN_NAME = ? AND sd.$COLUMN_DATE = ?
            ORDER BY s.$COLUMN_START_TIME
        """

        val cursor = db.rawQuery(query, arrayOf(
            groupName,
            date.format(DateTimeFormatter.ofPattern("dd.MM"))
        ))

        while (cursor.moveToNext()) {
            courses.add(createCourseFromCursor(cursor, date))
        }
        cursor.close()
        return courses
    }

    fun getScheduleForRoom(roomName: String, date: LocalDate): List<Course> {
        val courses = mutableListOf<Course>()
        val db = readableDatabase
        val query = """
            SELECT l.$COLUMN_SUBJECT, t.$COLUMN_NAME, l.$COLUMN_LESSON_TYPE, r.$COLUMN_NAME,
                   s.$COLUMN_START_TIME, s.$COLUMN_END_TIME, l.$COLUMN_SUBGROUP, g.$COLUMN_NAME
            FROM $TABLE_LESSONS l
            JOIN $TABLE_SESSIONS s ON l.$COLUMN_SESSION_ID = s.$COLUMN_ID
            JOIN $TABLE_GROUPS g ON s.$COLUMN_GROUP_ID = g.$COLUMN_ID
            JOIN $TABLE_TEACHERS t ON l.$COLUMN_TEACHER_ID = t.$COLUMN_ID
            JOIN $TABLE_ROOMS r ON l.$COLUMN_ROOM_ID = r.$COLUMN_ID
            JOIN $TABLE_SCHEDULE_DATES sd ON l.$COLUMN_ID = sd.$COLUMN_LESSON_ID
            WHERE r.$COLUMN_NAME = ? AND sd.$COLUMN_DATE = ?
            ORDER BY s.$COLUMN_START_TIME
        """

        val cursor = db.rawQuery(query, arrayOf(
            roomName,
            date.format(DateTimeFormatter.ofPattern("dd.MM"))
        ))

        while (cursor.moveToNext()) {
            courses.add(createCourseFromCursor(cursor, date))
        }
        cursor.close()
        return courses
    }

    fun getScheduleForTeacher(teacherName: String, date: LocalDate): List<Course> {
        val courses = mutableListOf<Course>()
        val db = readableDatabase
        val query = """
            SELECT l.$COLUMN_SUBJECT, t.$COLUMN_NAME, l.$COLUMN_LESSON_TYPE, r.$COLUMN_NAME,
                   s.$COLUMN_START_TIME, s.$COLUMN_END_TIME, l.$COLUMN_SUBGROUP, g.$COLUMN_NAME
            FROM $TABLE_LESSONS l
            JOIN $TABLE_SESSIONS s ON l.$COLUMN_SESSION_ID = s.$COLUMN_ID
            JOIN $TABLE_GROUPS g ON s.$COLUMN_GROUP_ID = g.$COLUMN_ID
            JOIN $TABLE_TEACHERS t ON l.$COLUMN_TEACHER_ID = t.$COLUMN_ID
            LEFT JOIN $TABLE_ROOMS r ON l.$COLUMN_ROOM_ID = r.$COLUMN_ID
            JOIN $TABLE_SCHEDULE_DATES sd ON l.$COLUMN_ID = sd.$COLUMN_LESSON_ID
            WHERE t.$COLUMN_NAME = ? AND sd.$COLUMN_DATE = ?
            ORDER BY s.$COLUMN_START_TIME
        """

        val cursor = db.rawQuery(query, arrayOf(
            teacherName,
            date.format(DateTimeFormatter.ofPattern("dd.MM"))
        ))

        while (cursor.moveToNext()) {
            courses.add(createCourseFromCursor(cursor, date))
        }
        cursor.close()
        return courses
    }

    private fun createCourseFromCursor(cursor: android.database.Cursor, date: LocalDate): Course {
        val startTimeStr = cursor.getString(4)
        val endTimeStr = cursor.getString(5)
        val startTime = LocalTime.parse(startTimeStr, DateTimeFormatter.ofPattern("HH:mm"))
        val endTime = LocalTime.parse(endTimeStr, DateTimeFormatter.ofPattern("HH:mm"))
        val duration = java.time.Duration.between(startTime, endTime)

        return Course(
            startTime = startTime,
            duration = duration,
            dates = listOf(date),
            groupName = cursor.getString(7),
            subject = cursor.getString(0),
            teacher = cursor.getString(1),
            type = cursor.getString(2),
            subgroup = if (cursor.isNull(6)) null else cursor.getString(6),
            cabinet = if (cursor.isNull(3)) null else cursor.getString(3)
        )
    }
} 