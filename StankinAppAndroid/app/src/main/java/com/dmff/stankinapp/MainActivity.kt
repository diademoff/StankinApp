package com.dmff.stankinapp

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.foundation.layout.*
import androidx.compose.material.*
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Home
import androidx.compose.material.icons.filled.Settings
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.core.splashscreen.SplashScreen.Companion.installSplashScreen
import com.dmff.stankinapp.data.db.ScheduleDatabase
import com.dmff.stankinapp.data.model.Course
import com.dmff.stankinapp.data.preferences.UserPreferences
import com.dmff.stankinapp.ui.schedule.ScheduleScreen
import com.dmff.stankinapp.ui.theme.StankinAppTheme
import kotlinx.coroutines.flow.collectLatest
import java.time.LocalDate

class MainActivity : ComponentActivity() {
    private lateinit var database: ScheduleDatabase
    private lateinit var userPreferences: UserPreferences

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        
        installSplashScreen()
        enableEdgeToEdge()

        database = ScheduleDatabase(this)
        userPreferences = UserPreferences(this)

        setContent {
            StankinAppTheme {
                Surface(
                    modifier = Modifier.fillMaxSize(),
                    color = MaterialTheme.colors.background
                ) {
                    var selectedGroup by remember { mutableStateOf<String?>(null) }
                    var schedule by remember { mutableStateOf<Map<LocalDate, List<Course>>>(emptyMap()) }
                    var loadedDates by remember { mutableStateOf(mutableSetOf<LocalDate>()) }
                    var listStartDate by remember { mutableStateOf(LocalDate.now().minusDays(7)) }
                    var listEndDate by remember { mutableStateOf(LocalDate.now().plusDays(7)) }
                    var currentDate by remember { mutableStateOf(LocalDate.now()) }
                    var scheduleUpdatedByDatePicker by remember { mutableStateOf(false) }

                    // Для навигации
                    var currentScreen by remember { mutableStateOf("schedule") } // "schedule" или "settings"

                    // Загрузка выбранной группы
                    LaunchedEffect(Unit) {
                        userPreferences.selectedGroup.collectLatest { group ->
                            selectedGroup = group
                        }
                    }

                    // Загрузка расписания при выборе группы или изменении текущей даты
                    LaunchedEffect(selectedGroup, currentDate) {
                        if (selectedGroup != null) {
                            val startDate = currentDate.minusDays(7)
                            val endDate = currentDate.plusDays(7)
                            for (date in startDate..endDate) {
                                if (date !in loadedDates) {
                                    loadedDates.add(date)
                                    val courses = database.getScheduleForGroup(selectedGroup!!, date)
                                    schedule = schedule + (date to courses)
                                }
                            }
                            listStartDate = startDate
                            listEndDate = endDate
                        }
                    }

                    // Подгрузка при прокручивании
                    LaunchedEffect(listStartDate, listEndDate) {
                        if (selectedGroup != null) {
                            val startDate = listStartDate
                            val endDate = listEndDate
                            for (date in startDate..endDate) {
                                if (date !in loadedDates) {
                                    loadedDates.add(date)
                                    val courses = database.getScheduleForGroup(selectedGroup!!, date)
                                    schedule = schedule + (date to courses)
                                }
                            }
                        }
                    }

                    Scaffold(
                        bottomBar = {
                            BottomNavigation(
                                backgroundColor = MaterialTheme.colors.surface,
                                contentColor = MaterialTheme.colors.onSurface
                            ) {
                                BottomNavigationItem(
                                    icon = { Icon(Icons.Filled.Home, contentDescription = "Домой") },
                                    //label = { Text("Домой") },
                                    selected = currentScreen == "schedule",
                                    onClick = { currentScreen = "schedule" }
                                )
                                BottomNavigationItem(
                                    icon = { Icon(Icons.Filled.Settings, contentDescription = "Настройки") },
                                    //label = { Text("Настройки") },
                                    selected = currentScreen == "settings",
                                    onClick = { currentScreen = "settings" }
                                )
                            }
                        }
                    ) { paddingValues ->
                        Column(modifier = Modifier.padding(paddingValues)) {
                            when (currentScreen) {
                                "schedule" -> ScheduleScreen(
                                    groupName = selectedGroup ?: "",
                                    schedule = schedule,
                                    currentDate = currentDate,
                                    onDateChange = { newDate ->
                                        currentDate = newDate
                                        scheduleUpdatedByDatePicker = true
                                    },
                                    onLoadMore = { date, isStart ->
                                        if (isStart) {
                                            listStartDate = date
                                        } else {
                                            listEndDate = date
                                        }
                                    },
                                    onNavigateBack = { finish() },
                                    onScheduleUpdatedByDatePicker = { scheduleUpdatedByDatePicker = true }
                                )
                                "settings" -> SettingsScreen()
                            }
                        }
                    }
                }
            }
        }
    }
}

// Заглушка для экрана настроек
@Composable
fun SettingsScreen() {
    Box(
        modifier = Modifier.fillMaxSize(),
        contentAlignment = Alignment.Center
    ) {
        Text("Настройки пока не реализованы")
    }
}

// Итератор для диапазона дат
operator fun ClosedRange<LocalDate>.iterator() = generateSequence(start) { it.plusDays(1) }
    .takeWhile { it <= endInclusive }
    .iterator()