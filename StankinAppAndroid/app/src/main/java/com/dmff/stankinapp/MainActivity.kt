package com.dmff.stankinapp

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.core.splashscreen.SplashScreen.Companion.installSplashScreen
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.lifecycle.lifecycleScope
import com.dmff.stankinapp.data.db.ScheduleDatabase
import com.dmff.stankinapp.data.model.Course
import com.dmff.stankinapp.data.preferences.UserPreferences
import com.dmff.stankinapp.ui.schedule.ScheduleScreen
import com.dmff.stankinapp.ui.theme.StankinAppTheme
import com.dmff.stankinapp.ui.welcome.WelcomeScreen
import kotlinx.coroutines.flow.collectLatest
import kotlinx.coroutines.launch
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
                    color = MaterialTheme.colorScheme.background
                ) {
                    var selectedGroup by remember { mutableStateOf<String?>(null) }
                    var schedule by remember { mutableStateOf<Map<LocalDate, List<Course>>>(emptyMap()) }
                    var loadedDates by remember { mutableStateOf(mutableSetOf<LocalDate>()) }
                    var listStartDate by remember { mutableStateOf(LocalDate.now().minusDays(7)) }
                    var listEndDate by remember { mutableStateOf(LocalDate.now().plusDays(7)) }
                    var currentDate by remember { mutableStateOf(LocalDate.now()) }

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

                    if (selectedGroup == null) {
                        WelcomeScreen(
                            groups = database.getGroups(),
                            onGroupSelected = { group ->
                                lifecycleScope.launch {
                                    userPreferences.setSelectedGroup(group)
                                }
                            }
                        )
                    } else {
                        ScheduleScreen(
                            groupName = selectedGroup!!,
                            schedule = schedule,
                            currentDate = currentDate,
                            onDateChange = { newDate ->
                                currentDate = newDate
                            },
                            onLoadMore = { date, isStart ->
                                if (isStart) {
                                    listStartDate = date
                                } else {
                                    listEndDate = date
                                }
                            },
                            onNavigateBack = { finish() }
                        )
                    }
                }
            }
        }
    }
}

// Итератор для диапазона дат
operator fun ClosedRange<LocalDate>.iterator() = generateSequence(start) { it.plusDays(1) }
    .takeWhile { it <= endInclusive }
    .iterator()