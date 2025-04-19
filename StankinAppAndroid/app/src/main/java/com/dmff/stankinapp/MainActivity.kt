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
import com.dmff.stankinapp.data.db.DatabaseBuilder
import com.dmff.stankinapp.data.model.Course
import com.dmff.stankinapp.ui.schedule.ScheduleScreen
import com.dmff.stankinapp.ui.theme.StankinAppTheme
import kotlinx.coroutines.launch
import java.time.LocalDate

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        
        val databaseBuilder = DatabaseBuilder(this)
        
        setContent {
            StankinAppTheme {
                Surface(
                    modifier = Modifier.fillMaxSize(),
                    color = MaterialTheme.colorScheme.background
                ) {
                    var schedule by remember { mutableStateOf<List<Course>>(emptyList()) }
                    val scope = rememberCoroutineScope()
                    
                    LaunchedEffect(Unit) {
                        scope.launch {
                            schedule = databaseBuilder.getScheduleForGroup("YOUR_GROUP_NAME", LocalDate.now())
                        }
                    }
                    
                    ScheduleScreen(
                        groupName = "YOUR_GROUP_NAME",
                        schedule = schedule,
                        onDateChange = { newDate ->
                            scope.launch {
                                schedule = databaseBuilder.getScheduleForGroup("YOUR_GROUP_NAME", newDate)
                            }
                        },
                        onNavigateBack = { finish() }
                    )
                }
            }
        }
    }
}