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
                    var schedule by remember { mutableStateOf<List<Course>>(emptyList()) }
                    
                    LaunchedEffect(Unit) {
                        schedule = database.getScheduleForGroup("АДБ-23-07", LocalDate.now())
                    }
                    
                    ScheduleScreen(
                        groupName = "АДБ-23-07",
                        schedule = schedule,
                        onDateChange = { newDate ->
                            schedule = database.getScheduleForGroup("АДБ-23-07", newDate)
                        },
                        onNavigateBack = { finish() }
                    )
                }
            }
        }
    }
}