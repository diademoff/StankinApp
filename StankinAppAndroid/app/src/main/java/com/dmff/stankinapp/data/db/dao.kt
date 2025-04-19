package com.dmff.stankinapp.data.db

import androidx.room.*
import com.dmff.stankinapp.data.model.Course
import java.time.LocalDate
import java.time.LocalTime
import java.time.format.DateTimeFormatter

@Dao
interface GroupDao {
    @Query("SELECT name FROM groups ORDER BY name")
    suspend fun getAllGroups(): List<String>

    @Insert(onConflict = OnConflictStrategy.IGNORE)
    suspend fun insertGroup(group: GroupEntity): Long

    @Query("SELECT id FROM groups WHERE name = :name")
    suspend fun getGroupIdByName(name: String): Long?
}

@Dao
interface TeacherDao {
    @Query("SELECT name FROM teachers ORDER BY name")
    suspend fun getAllTeachers(): List<String>

    @Insert(onConflict = OnConflictStrategy.IGNORE)
    suspend fun insertTeacher(teacher: TeacherEntity): Long

    @Query("SELECT id FROM teachers WHERE name = :name")
    suspend fun getTeacherIdByName(name: String): Long?
}

@Dao
interface RoomDao {
    @Query("SELECT name FROM rooms ORDER BY name")
    suspend fun getAllRooms(): List<String>

    @Insert(onConflict = OnConflictStrategy.IGNORE)
    suspend fun insertRoom(room: RoomEntity): Long

    @Query("SELECT id FROM rooms WHERE name = :name")
    suspend fun getRoomIdByName(name: String): Long?
}

@Dao
interface SessionDao {
    @Insert
    suspend fun insertSession(session: SessionEntity): Long
}

@Dao
interface LessonDao {
    @Insert
    suspend fun insertLesson(lesson: LessonEntity): Long
}

@Dao
interface ScheduleDateDao {
    @Insert
    suspend fun insertScheduleDate(date: ScheduleDateEntity)

    @Query("""
        SELECT 
            l.subject as subject,
            t.name as teacher,
            l.lesson_type as type,
            r.name as cabinet,
            s.start_time as startTime,
            s.end_time as endTime,
            l.subgroup as subgroup,
            g.name as groupName
        FROM lessons l
        JOIN sessions s ON l.session_id = s.id
        JOIN groups g ON s.group_id = g.id
        JOIN teachers t ON l.teacher_id = t.id
        LEFT JOIN rooms r ON l.room_id = r.id
        JOIN schedule_dates sd ON l.id = sd.lesson_id
        WHERE g.name = :groupName AND sd.date = :date
        ORDER BY s.start_time
    """)
    suspend fun getScheduleForGroup(groupName: String, date: String): List<Course>

    @Query("""
        SELECT 
            l.subject as subject,
            t.name as teacher,
            l.lesson_type as type,
            r.name as cabinet,
            s.start_time as startTime,
            s.end_time as endTime,
            l.subgroup as subgroup,
            g.name as groupName
        FROM lessons l
        JOIN sessions s ON l.session_id = s.id
        JOIN groups g ON s.group_id = g.id
        JOIN teachers t ON l.teacher_id = t.id
        JOIN rooms r ON l.room_id = r.id
        JOIN schedule_dates sd ON l.id = sd.lesson_id
        WHERE r.name = :roomName AND sd.date = :date
        ORDER BY s.start_time
    """)
    suspend fun getScheduleForRoom(roomName: String, date: String): List<Course>

    @Query("""
        SELECT 
            l.subject as subject,
            t.name as teacher,
            l.lesson_type as type,
            r.name as cabinet,
            s.start_time as startTime,
            s.end_time as endTime,
            l.subgroup as subgroup,
            g.name as groupName
        FROM lessons l
        JOIN sessions s ON l.session_id = s.id
        JOIN groups g ON s.group_id = g.id
        JOIN teachers t ON l.teacher_id = t.id
        LEFT JOIN rooms r ON l.room_id = r.id
        JOIN schedule_dates sd ON l.id = sd.lesson_id
        WHERE t.name = :teacherName AND sd.date = :date
        ORDER BY s.start_time
    """)
    suspend fun getScheduleForTeacher(teacherName: String, date: String): List<Course>
} 