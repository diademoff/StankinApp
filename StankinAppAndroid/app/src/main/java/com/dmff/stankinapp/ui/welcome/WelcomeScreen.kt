import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.clickable
import androidx.compose.material.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp

@Composable
fun WelcomeScreen(
    groups: List<String>,
    onGroupSelected: (String) -> Unit
) {
    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Выберите группу") }
            )
        }
    ) { padding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding)
                .padding(horizontal = 16.dp),
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.spacedBy(16.dp)
        ) {
            Text(
                text = "Добро пожаловать в StankinApp!",
                style = MaterialTheme.typography.h6, // исправлено на h6
                textAlign = TextAlign.Center,
                modifier = Modifier.padding(top = 32.dp)
            )
            
            Text(
                text = "Пожалуйста, выберите вашу учебную группу",
                style = MaterialTheme.typography.body1, // исправлено на body1
                textAlign = TextAlign.Center
            )

            LazyColumn(
                modifier = Modifier
                    .fillMaxWidth()
                    .weight(1f),
                verticalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                items(groups) { group ->
                    GroupItem(
                        groupName = group,
                        onClick = { onGroupSelected(group) }
                    )
                }
            }
        }
    }
}

@Composable
fun GroupItem(
    groupName: String,
    onClick: () -> Unit
) {
    Card(
        modifier = Modifier
            .fillMaxWidth()
            .clickable(onClick = onClick),
        elevation = 2.dp
    ) {
        Text(
            text = groupName,
            style = MaterialTheme.typography.subtitle1,
            modifier = Modifier
                .fillMaxWidth()
                .padding(16.dp),
            textAlign = TextAlign.Center
        )
    }
}