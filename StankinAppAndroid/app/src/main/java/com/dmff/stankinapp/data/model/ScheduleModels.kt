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