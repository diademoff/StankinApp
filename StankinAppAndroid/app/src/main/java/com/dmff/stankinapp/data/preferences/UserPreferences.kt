package com.dmff.stankinapp.data.preferences

import android.content.Context
import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.stringPreferencesKey
import androidx.datastore.preferences.preferencesDataStore
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map

private val Context.dataStore: DataStore<Preferences> by preferencesDataStore(name = "user_preferences")

class UserPreferences(private val context: Context) {
    private val SELECTED_GROUP_KEY = stringPreferencesKey("selected_group")

    val selectedGroup: Flow<String?> = context.dataStore.data
        .map { preferences ->
            preferences[SELECTED_GROUP_KEY]
        }

    suspend fun setSelectedGroup(group: String) {
        context.dataStore.edit { preferences ->
            preferences[SELECTED_GROUP_KEY] = group
        }
    }
} 