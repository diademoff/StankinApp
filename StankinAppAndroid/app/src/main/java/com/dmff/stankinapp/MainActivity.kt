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
                    var schedule by remember { mutableStateOf<Map<LocalDate, List<Course>>>(emptyMap()) }
                    var currentDate by remember { mutableStateOf(LocalDate.now()) }
                    
                    LaunchedEffect(currentDate) {
                        val dates = (-7..7).map { currentDate.plusDays(it.toLong()) }
                        val newSchedule = dates.associateWith { date ->
                            database.getScheduleForGroup("АДБ-23-07", date)
                        }
                        schedule = newSchedule
                    }
                    
                    ScheduleScreen(
                        groupName = "АДБ-23-07",
                        schedule = schedule,
                        currentDate = currentDate,
                        onDateChange = { newDate ->
                            currentDate = newDate
                        },
                        onNavigateBack = { finish() }
                    )
                }
            }
        }
    }
}