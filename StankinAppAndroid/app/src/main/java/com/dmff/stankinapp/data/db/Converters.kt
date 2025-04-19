package com.dmff.stankinapp.data.db

import androidx.room.TypeConverter
import com.google.gson.Gson
import com.google.gson.reflect.TypeToken
import java.time.Duration
import java.time.LocalDate
import java.time.LocalTime
import java.time.format.DateTimeFormatter

class Converters {
    private val gson = Gson()

    @TypeConverter
    fun fromTimestamp(value: String?): LocalTime? {
        return value?.let { LocalTime.parse(it, DateTimeFormatter.ofPattern("HH:mm")) }
    }

    @TypeConverter
    fun dateToTimestamp(date: LocalTime?): String? {
        return date?.format(DateTimeFormatter.ofPattern("HH:mm"))
    }

    @TypeConverter
    fun fromDateString(value: String?): LocalDate? {
        return value?.let { LocalDate.parse(it, DateTimeFormatter.ofPattern("dd.MM")) }
    }

    @TypeConverter
    fun dateToDateString(date: LocalDate?): String? {
        return date?.format(DateTimeFormatter.ofPattern("dd.MM"))
    }

    @TypeConverter
    fun fromDurationString(value: String?): Duration? {
        return value?.let { 
            val parts = it.split(":")
            Duration.ofHours(parts[0].toLong()).plusMinutes(parts[1].toLong())
        }
    }

    @TypeConverter
    fun durationToString(duration: Duration?): String? {
        return duration?.let {
            "${it.toHours()}:${it.toMinutesPart()}"
        }
    }

    @TypeConverter
    fun fromDateListString(value: String?): List<LocalDate>? {
        return value?.let {
            val type = object : TypeToken<List<String>>() {}.type
            val dateStrings = gson.fromJson<List<String>>(it, type)
            dateStrings.map { dateStr -> 
                LocalDate.parse(dateStr, DateTimeFormatter.ofPattern("dd.MM"))
            }
        }
    }

    @TypeConverter
    fun dateListToString(dates: List<LocalDate>?): String? {
        return dates?.let {
            val dateStrings = it.map { date -> 
                date.format(DateTimeFormatter.ofPattern("dd.MM"))
            }
            gson.toJson(dateStrings)
        }
    }
} 