package com.dmff.stankinapp

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import com.dmff.stankinapp.data.db.ScheduleDatabase
import com.dmff.stankinapp.data.model.Course
import com.dmff.stankinapp.ui.schedule.ScheduleScreen
import com.dmff.stankinapp.ui.theme.StankinAppTheme
import java.time.LocalDate

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()

        val database = ScheduleDatabase(this)

        setContent {
            StankinAppTheme {
                Surface(
                    modifier = Modifier.fillMaxSize(),
                    color = MaterialTheme.colorScheme.background
                ) {
                    val schedule = remember { mutableStateMapOf<LocalDate, List<Course>>() }
                    val loadedDates = remember { mutableStateOf(mutableSetOf<LocalDate>()) }
                    val listStartDate = remember { mutableStateOf(LocalDate.now().minusDays(7)) }
                    val listEndDate = remember { mutableStateOf(LocalDate.now().plusDays(7)) }
                    val currentDate = remember { mutableStateOf(LocalDate.now()) }

                    LaunchedEffect(listStartDate.value, listEndDate.value) {
                        for (date in listStartDate.value..listEndDate.value) {
                            if (date !in loadedDates.value) {
                                loadedDates.value.add(date)
                                val courses = database.getScheduleForGroup("АДБ-23-07", date)
                                schedule[date] = courses
                            }
                        }
                    }

                    ScheduleScreen(
                        groupName = "АДБ-23-07",
                        schedule = schedule.toMap(),
                        currentDate = currentDate.value,
                        onDateChange = { newDate -> currentDate.value = newDate },
                        onLoadMore = { date, isStart ->
                            if (isStart) {
                                listStartDate.value = date
                            } else {
                                listEndDate.value = date
                            }
                        },
                        onNavigateBack = { finish() }
                    )
                }
            }
        }
    }
}

// helper for LocalDate range
operator fun ClosedRange<LocalDate>.iterator() = generateSequence(start) { it.plusDays(1) }
    .takeWhile { it <= endInclusive }
    .iterator()