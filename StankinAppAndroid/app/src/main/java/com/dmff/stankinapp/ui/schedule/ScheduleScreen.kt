package com.dmff.stankinapp.ui.schedule

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyListState
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.ArrowBack
import androidx.compose.material.icons.filled.CalendarToday
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import com.dmff.stankinapp.data.model.Course
import java.time.LocalDate
import java.time.format.DateTimeFormatter

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun ScheduleScreen(
    groupName: String,
    schedule: Map<LocalDate, List<Course>>,
    currentDate: LocalDate,
    onDateChange: (LocalDate) -> Unit,
    onNavigateBack: () -> Unit
) {
    val listState = rememberLazyListState()
    
    LaunchedEffect(listState) {
        snapshotFlow { listState.firstVisibleItemIndex }
            .collect { index ->
                if (index == 0) {
                    onDateChange(currentDate.minusDays(7))
                } else if (index == schedule.size - 1) {
                    onDateChange(currentDate.plusDays(7))
                }
            }
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text(groupName) },
                navigationIcon = {
                    IconButton(onClick = onNavigateBack) {
                        Icon(Icons.Default.ArrowBack, contentDescription = "Back")
                    }
                },
                actions = {
                    IconButton(onClick = { /* TODO: Show date picker */ }) {
                        Icon(Icons.Default.CalendarToday, contentDescription = "Select date")
                    }
                }
            )
        }
    ) { padding ->
        if (schedule.isEmpty()) {
            Box(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(padding),
                contentAlignment = Alignment.Center
            ) {
                Text("No schedule available")
            }
        } else {
            LazyColumn(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(padding)
                    .padding(horizontal = 16.dp),
                state = listState,
                verticalArrangement = Arrangement.spacedBy(16.dp)
            ) {
                items(schedule.entries.toList()) { (date, courses) ->
                    DaySchedule(date, courses)
                }
            }
        }
    }
}

@Composable
fun DaySchedule(date: LocalDate, courses: List<Course>) {
    Column(
        modifier = Modifier.fillMaxWidth(),
        verticalArrangement = Arrangement.spacedBy(8.dp)
    ) {
        Text(
            text = date.format(DateTimeFormatter.ofPattern("EEEE, d MMMM")),
            style = MaterialTheme.typography.titleLarge
        )
        
        if (courses.isEmpty()) {
            Text(
                text = "No classes scheduled",
                style = MaterialTheme.typography.bodyMedium
            )
        } else {
            courses.forEach { course ->
                ScheduleCard(course = course)
            }
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun ScheduleCard(course: Course) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        elevation = CardDefaults.cardElevation(defaultElevation = 2.dp)
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            // Time and subject
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text(
                    text = "${course.startTime.format(DateTimeFormatter.ofPattern("HH:mm"))} - " +
                           "${course.startTime.plus(course.duration!!).format(DateTimeFormatter.ofPattern("HH:mm"))}",
                    style = MaterialTheme.typography.titleMedium
                )
                Text(
                    text = course.subject ?: "",
                    style = MaterialTheme.typography.titleLarge,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis
                )
            }

            // Type and subgroup
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween
            ) {
                Text(
                    text = course.type ?: "",
                    style = MaterialTheme.typography.bodyMedium
                )
                if (!course.subgroup.isNullOrEmpty()) {
                    Text(
                        text = "Subgroup ${course.subgroup}",
                        style = MaterialTheme.typography.bodyMedium
                    )
                }
            }

            // Teacher and room
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween
            ) {
                Text(
                    text = course.teacher ?: "",
                    style = MaterialTheme.typography.bodyMedium
                )
                if (!course.cabinet.isNullOrEmpty()) {
                    Text(
                        text = "Room ${course.cabinet}",
                        style = MaterialTheme.typography.bodyMedium
                    )
                }
            }
        }
    }
}