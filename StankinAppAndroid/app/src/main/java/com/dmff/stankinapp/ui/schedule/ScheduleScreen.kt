package com.dmff.stankinapp.ui.schedule

import androidx.compose.animation.animateContentSize
import androidx.compose.animation.core.tween
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
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
import kotlinx.coroutines.flow.distinctUntilChanged
import kotlinx.coroutines.flow.filter

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun ScheduleScreen(
    groupName: String,
    schedule: Map<LocalDate, List<Course>>,
    currentDate: LocalDate,
    onDateChange: (LocalDate) -> Unit,
    onLoadMore: (LocalDate, isStart: Boolean) -> Unit,
    onNavigateBack: () -> Unit
) {
    val listState = rememberLazyListState()
    var initialScrollDone by remember { mutableStateOf(false) }

    // Прокручивание до сегодняшней даты при запуске
    LaunchedEffect(schedule) {
        if (!initialScrollDone && schedule.isNotEmpty()) {
            val sortedDates = schedule.keys.toList().sorted()
            val index = sortedDates.indexOfFirst { it == currentDate }
            if (index != -1) {
                listState.scrollToItem(index)
                initialScrollDone = true
            }
        }
    }

    // Логика для подгрузки данных при прокрутке
    val sortedSchedule = remember(schedule) { schedule.toSortedMap() }
    val totalCount = sortedSchedule.size

    LaunchedEffect(listState, schedule) {
        snapshotFlow { listState.layoutInfo.visibleItemsInfo }
            .filter { it.isNotEmpty() }
            .distinctUntilChanged()
            .collect { visibleItems ->
                if (visibleItems.isNotEmpty() && sortedSchedule.isNotEmpty()) {
                    val firstVisibleIndex = visibleItems.first().index
                    val lastVisibleIndex = visibleItems.last().index
                    val totalCount = sortedSchedule.size

                    // Подгрузка при скролле вверх
                    if (firstVisibleIndex <= 3) {
                        val firstDate = sortedSchedule.keys.firstOrNull()
                        if (firstDate != null) {
                            onLoadMore(firstDate.minusDays(7), true)
                        }
                    }

                    // Подгрузка при скролле вниз
                    if (lastVisibleIndex >= totalCount - 3) {
                        val lastDate = sortedSchedule.keys.lastOrNull()
                        if (lastDate != null) {
                            onLoadMore(lastDate.plusDays(7), false)
                        }
                    }
                } else {
                    println("visibleItems или sortedSchedule пусты")
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
                items(
                    items = sortedSchedule.entries.toList(),
                    key = { it.key }
                ) { (date, courses) ->
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
        modifier = Modifier
            .fillMaxWidth()
            .animateContentSize(animationSpec = tween(durationMillis = 250)),
        elevation = CardDefaults.cardElevation(defaultElevation = 2.dp),
        shape = MaterialTheme.shapes.medium,
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceVariant)
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(12.dp),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            Column(
                horizontalAlignment = Alignment.CenterHorizontally,
                verticalArrangement = Arrangement.Center,
                modifier = Modifier.width(64.dp)
            ) {
                Text(
                    text = course.startTime.format(DateTimeFormatter.ofPattern("HH:mm")),
                    style = MaterialTheme.typography.labelLarge
                )
                Text(
                    text = course.startTime
                        .plus(course.duration!!)
                        .format(DateTimeFormatter.ofPattern("HH:mm")),
                    style = MaterialTheme.typography.labelLarge,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }

            Column(
                verticalArrangement = Arrangement.spacedBy(4.dp),
                modifier = Modifier.weight(1f)
            ) {
                Text(
                    text = course.subject.orEmpty(),
                    style = MaterialTheme.typography.titleMedium,
                    maxLines = 2,
                    overflow = TextOverflow.Ellipsis
                )

                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    if (!course.type.isNullOrEmpty()) {
                        Text(
                            text = course.type,
                            style = MaterialTheme.typography.bodySmall
                        )
                    }
                    if (!course.subgroup.isNullOrEmpty()) {
                        Text(
                            text = "п/г ${course.subgroup}",
                            style = MaterialTheme.typography.bodySmall
                        )
                    }
                }

                Row(
                    horizontalArrangement = Arrangement.SpaceBetween,
                    modifier = Modifier.fillMaxWidth()
                ) {
                    if (!course.teacher.isNullOrEmpty()) {
                        Text(
                            text = course.teacher,
                            style = MaterialTheme.typography.bodySmall
                        )
                    }
                    if (!course.cabinet.isNullOrEmpty()) {
                        Text(
                            text = course.cabinet,
                            style = MaterialTheme.typography.bodySmall
                        )
                    }
                }
            }
        }
    }
}