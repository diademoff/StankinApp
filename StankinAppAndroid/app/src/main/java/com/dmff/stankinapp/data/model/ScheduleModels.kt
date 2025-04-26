package com.dmff.stankinapp.data.model

import java.time.LocalDate
import java.time.LocalTime
import java.time.Duration

data class Course(
    val startTime: LocalTime,
    val duration: Duration?,
    val dates: List<LocalDate>?,
    val groupName: String?,
    val subject: String?,
    val teacher: String?,
    val type: String?,
    val subgroup: String?,
    val cabinet: String?
) {
    override fun toString(): String {
        if (duration == null || dates == null) {
            throw Exception("Course Duration or Dates is null")
        }
        val endTime = startTime.plus(duration)
        val datesStr = dates.joinToString(", ") { it.format(java.time.format.DateTimeFormatter.ofPattern("dd.MM")) }
        val subgroupInfo = if (!subgroup.isNullOrEmpty()) " ($subgroup)" else ""
        val cabinetInfo = if (!cabinet.isNullOrEmpty()) " в $cabinet" else ""

        return "${startTime.format(java.time.format.DateTimeFormatter.ofPattern("HH:mm"))}-" +
               "${endTime.format(java.time.format.DateTimeFormatter.ofPattern("HH:mm"))} | " +
               "$subject$subgroupInfo | $type | $teacher$cabinetInfo | $datesStr"
    }
}

data class Schedule(
    val groupName: String,
    val days: List<Course>
) {
    init {
        require(days.isNotEmpty()) { "Days list cannot be empty" }
    }
}

// Функция для объединения последовательных курсов
fun mergeConsecutiveCourses(courses: List<Course>): List<Course> {
    if (courses.isEmpty()) return emptyList()
    val merged = mutableListOf<Course>()
    var current = courses[0]
    for (nextCourse in courses.drop(1)) {
        if (canMerge(current, nextCourse)) {
            current = mergeCourses(current, nextCourse)
        } else {
            merged.add(current)
            current = nextCourse
        }
    }
    merged.add(current)
    return merged
}

private fun canMerge(course1: Course, course2: Course): Boolean {
    return course1.subject == course2.subject &&
           course1.teacher == course2.teacher &&
           course1.cabinet == course2.cabinet &&
           course1.type == course2.type &&
           course1.subgroup == course2.subgroup
}

private fun mergeCourses(course1: Course, course2: Course): Course {
    val newDuration = course1.duration?.plus(course2.duration ?: Duration.ZERO) ?: course2.duration
    val newDates = (course1.dates ?: emptyList()) + (course2.dates ?: emptyList())
    return Course(
        startTime = course1.startTime,
        duration = newDuration,
        dates = newDates,
        groupName = course1.groupName,
        subject = course1.subject,
        teacher = course1.teacher,
        type = course1.type,
        subgroup = course1.subgroup,
        cabinet = course1.cabinet
    )
}

fun mergeCoursesByAttributes(courses: List<Course>): List<Course> {
    // Группируем по ключам: subject, teacher, cabinet, type, subgroup
    val grouped = courses.groupBy { course ->
        Triple(
            course.subject,
            course.teacher,
            course.cabinet
        )
    }

    // Объединяем каждую группу в один курс
    return grouped.values.map { group ->
        val first = group.first()
        val startTime = group.minOf { it.startTime }
        val endTimes = group.mapNotNull { course ->
            course.startTime.plus(course.duration ?: Duration.ZERO)
        }
        val maxEndTime = endTimes.maxOrNull() ?: startTime.plus(first.duration ?: Duration.ZERO)
        val totalDuration = Duration.between(startTime, maxEndTime)
        val allDates = group.flatMap { it.dates ?: emptyList() }.distinct()

        // Создаём объединённый курс
        first.copy(
            startTime = startTime,
            duration = totalDuration,
            dates = allDates
        )
    }
}