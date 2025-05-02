package com.dmff.stankinapp.ui.schedule

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.foundation.background
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.ArrowBack
import androidx.compose.material.icons.filled.CalendarToday
import androidx.compose.material.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import com.dmff.stankinapp.data.model.Course
import com.dmff.stankinapp.data.model.mergeConsecutiveCourses
import com.dmff.stankinapp.data.model.mergeCoursesByAttributes
import java.time.LocalDate
import java.time.format.DateTimeFormatter
import kotlinx.coroutines.flow.distinctUntilChanged
import kotlinx.coroutines.flow.filter

@Composable
fun ScheduleScreen(
    groupName: String,
    schedule: Map<LocalDate, List<Course>>,
    currentDate: LocalDate,
    onDateChange: (LocalDate) -> Unit,
    onLoadMore: (LocalDate, isStart: Boolean) -> Unit,
    onNavigateBack: () -> Unit,
    onScheduleUpdatedByDatePicker: () -> Unit
) {
    val listState = rememberLazyListState()
    var showDatePicker by remember { mutableStateOf(false) }
    val scheduleUpdatedByDatePicker = remember { mutableStateOf(true) }

    // Прокрутка к выбранной дате после выбора даты в DatePicker
    LaunchedEffect(currentDate, schedule) {
        if (scheduleUpdatedByDatePicker.value) {
            val sortedDates = schedule.keys.toList().sorted()
            val index = sortedDates.indexOf(currentDate)
            if (index != -1) {
                listState.scrollToItem(index)
                scheduleUpdatedByDatePicker.value = false
            }
        }
    }

    // Логика подгрузки данных при прокрутке
    val sortedSchedule = remember(schedule) { schedule.toSortedMap() }
    LaunchedEffect(listState, schedule) {
        snapshotFlow { listState.layoutInfo.visibleItemsInfo }
            .filter { it.isNotEmpty() }
            .distinctUntilChanged()
            .collect { visibleItems ->
                if (visibleItems.isNotEmpty() && sortedSchedule.isNotEmpty()) {
                    val firstVisibleIndex = visibleItems.first().index
                    val lastVisibleIndex = visibleItems.last().index
                    val totalCount = sortedSchedule.size

                    if (firstVisibleIndex <= 3) {
                        val firstDate = sortedSchedule.keys.firstOrNull()
                        if (firstDate != null) {
                            onLoadMore(firstDate.minusDays(7), true)
                        }
                    }
                    if (lastVisibleIndex >= totalCount - 3) {
                        val lastDate = sortedSchedule.keys.lastOrNull()
                        if (lastDate != null) {
                            onLoadMore(lastDate.plusDays(7), false)
                        }
                    }
                }
            }
    }

    Scaffold(
        topBar = {
            Column {
                Spacer(Modifier
                    .windowInsetsTopHeight(WindowInsets.statusBars)
                    .background(MaterialTheme.colors.primary))

                TopAppBar(
                    title = { Text(groupName) },
                    navigationIcon = {
                        IconButton(onClick = onNavigateBack) {
                            Icon(Icons.Filled.ArrowBack, contentDescription = "Back")
                        }
                    },
                    actions = {
                        IconButton(onClick = { showDatePicker = true }) {
                            Icon(Icons.Filled.CalendarToday, contentDescription = "Select date")
                        }
                    }
                )
            }
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
                    // Объединяем курсы перед отображением
                    val mergedCourses = mergeCoursesByAttributes(courses)
                    DaySchedule(date, mergedCourses)
                }
            }
        }
    }

    // Диалог выбора даты
    if (showDatePicker) {
        val context = LocalContext.current
        val picker = remember {
            android.app.DatePickerDialog(
                context,
                { _, year, month, dayOfMonth ->
                    val selectedDate = LocalDate.of(year, month + 1, dayOfMonth)
                    onDateChange(selectedDate)
                    scheduleUpdatedByDatePicker.value = true
                    showDatePicker = false
                },
                currentDate.year,
                currentDate.monthValue - 1,
                currentDate.dayOfMonth
            )
        }
        LaunchedEffect(Unit) {
            picker.show()
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
            style = MaterialTheme.typography.subtitle1
        )

        if (courses.isEmpty()) {
            Text(
                text = "Нет расписания",
                style = MaterialTheme.typography.body2,
                modifier = Modifier.fillMaxWidth(),
                textAlign = TextAlign.Center
            )
        } else {
            courses.forEach { course ->
                ScheduleCard(course = course)
            }
        }
    }
}

@Composable
fun ScheduleCard(course: Course) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        elevation = 2.dp,
        shape = MaterialTheme.shapes.medium,
        backgroundColor = MaterialTheme.colors.surface // заменено surfaceVariant
    ){
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
                    style = MaterialTheme.typography.subtitle1 // заменено titleLarge
                )
                Text(
                    text = course.startTime
                        .plus(course.duration)
                        .format(DateTimeFormatter.ofPattern("HH:mm")),
                    style = MaterialTheme.typography.subtitle1, // заменено titleLarge
                    color = MaterialTheme.colors.onSurface // заменено surfaceVariant
                )
            }

            Column(
                verticalArrangement = Arrangement.spacedBy(4.dp),
                modifier = Modifier.weight(1f)
            ) {
                Text(
                    text = course.subject.orEmpty(),
                    style = MaterialTheme.typography.subtitle1, // заменено titleMedium
                    maxLines = 2,
                    overflow = TextOverflow.Ellipsis
                )

                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    if (!course.type.isNullOrEmpty()) {
                        Text(
                            text = course.type,
                            style = MaterialTheme.typography.caption // заменено bodySmall
                        )
                    }
                    if (!course.subgroup.isNullOrEmpty()) {
                        Text(
                            text = "п/г ${course.subgroup}",
                            style = MaterialTheme.typography.caption // заменено bodySmall
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
                            style = MaterialTheme.typography.caption // заменено bodySmall
                        )
                    }
                    if (!course.cabinet.isNullOrEmpty()) {
                        Text(
                            text = course.cabinet,
                            style = MaterialTheme.typography.caption // заменено bodySmall
                        )
                    }
                }
            }
        }
    }
}