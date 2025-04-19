package com.dmff.stankinapp.data.db

import android.content.Context
import androidx.room.Database
import androidx.room.Room
import androidx.room.RoomDatabase
import androidx.room.TypeConverters
import com.dmff.stankinapp.data.model.Course
import com.dmff.stankinapp.data.model.Schedule
import java.io.File
import java.io.FileOutputStream
import java.io.IOException
import java.time.LocalDate
import java.time.LocalTime
import java.time.format.DateTimeFormatter

@Database(
    entities = [
        GroupEntity::class,
        TeacherEntity::class,
        RoomEntity::class,
        SessionEntity::class,
        LessonEntity::class,
        ScheduleDateEntity::class
    ],
    version = 1
)
@TypeConverters(Converters::class)
abstract class ScheduleDatabase : RoomDatabase() {
    abstract fun groupDao(): GroupDao
    abstract fun teacherDao(): TeacherDao
    abstract fun roomDao(): RoomDao
    abstract fun sessionDao(): SessionDao
    abstract fun lessonDao(): LessonDao
    abstract fun scheduleDateDao(): ScheduleDateDao

    companion object {
        @Volatile
        private var INSTANCE: ScheduleDatabase? = null

        fun getDatabase(context: Context): ScheduleDatabase {
            return INSTANCE ?: synchronized(this) {
                val instance = Room.databaseBuilder(
                    context.applicationContext,
                    ScheduleDatabase::class.java,
                    "schedule.db"
                ).build()
                INSTANCE = instance
                instance
            }
        }
    }
}

class DatabaseBuilder(private val context: Context) {
    private val database = ScheduleDatabase.getDatabase(context)
    private val groupDao = database.groupDao()
    private val teacherDao = database.teacherDao()
    private val roomDao = database.roomDao()
    private val sessionDao = database.sessionDao()
    private val lessonDao = database.lessonDao()
    private val scheduleDateDao = database.scheduleDateDao()

    init {
        copyDatabaseFromAssets()
    }

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

    suspend fun processSchedule(schedule: Schedule) {
        // Insert or get group ID
        val groupId = groupDao.insertGroup(GroupEntity(name = schedule.groupName))

        // Process teachers and rooms
        val teachers = schedule.days.mapNotNull { it.teacher }.distinct()
        val rooms = schedule.days.mapNotNull { it.cabinet }.distinct()

        val teacherIds = teachers.associateWith { teacher ->
            teacherDao.insertTeacher(TeacherEntity(name = teacher))
        }

        val roomIds = rooms.associateWith { room ->
            roomDao.insertRoom(RoomEntity(name = room))
        }

        // Process each course
        for (course in schedule.days) {
            // Insert session
            val sessionId = sessionDao.insertSession(
                SessionEntity(
                    group_id = groupId,
                    start_time = course.startTime.format(DateTimeFormatter.ofPattern("HH:mm")),
                    end_time = course.startTime.plus(course.duration!!)
                        .format(DateTimeFormatter.ofPattern("HH:mm"))
                )
            )

            // Insert lesson
            val teacherId = teacherIds[course.teacher] ?: continue
            val roomId = course.cabinet?.let { roomIds[it] }

            val lessonId = lessonDao.insertLesson(
                LessonEntity(
                    session_id = sessionId,
                    subject = course.subject ?: "",
                    teacher_id = teacherId,
                    lesson_type = course.type ?: "",
                    room_id = roomId,
                    subgroup = course.subgroup
                )
            )

            // Insert schedule dates
            course.dates?.forEach { date ->
                scheduleDateDao.insertScheduleDate(
                    ScheduleDateEntity(
                        lesson_id = lessonId,
                        date = date.format(DateTimeFormatter.ofPattern("dd.MM"))
                    )
                )
            }
        }
    }

    suspend fun getGroups(): List<String> = groupDao.getAllGroups()

    suspend fun getRooms(): List<String> = roomDao.getAllRooms()

    suspend fun getTeachers(): List<String> = teacherDao.getAllTeachers()

    suspend fun getScheduleForGroup(groupName: String, date: LocalDate): List<Course> =
        scheduleDateDao.getScheduleForGroup(
            groupName,
            date.format(DateTimeFormatter.ofPattern("dd.MM"))
        )

    suspend fun getScheduleForRoom(roomName: String, date: LocalDate): List<Course> =
        scheduleDateDao.getScheduleForRoom(
            roomName,
            date.format(DateTimeFormatter.ofPattern("dd.MM"))
        )

    suspend fun getScheduleForTeacher(teacherName: String, date: LocalDate): List<Course> =
        scheduleDateDao.getScheduleForTeacher(
            teacherName,
            date.format(DateTimeFormatter.ofPattern("dd.MM"))
        )
} 