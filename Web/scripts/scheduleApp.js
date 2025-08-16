import ApiClient from './apiClient.js';
import LocalCache from './cache.js';
import SCHEDULE_CONFIG from './config.js';
import ScheduleRepository from './scheduleRepository.js';

/* ==========================
   Alpine root component: scheduleApp()
   - получает список групп
   - хранит выбранную группу
   ========================== */
export const scheduleApp = function () {
    const api = new ApiClient(window.API_BASE_URL || '');
    const cache = new LocalCache(SCHEDULE_CONFIG.CACHE_PREFIX);
    const repo = new ScheduleRepository(api, cache);

    return {
        groups: [],
        selectedGroup: null,
        loadingGroups: false,
        error: null,

        init() {
            this.loadGroups();
            // восстановим выбранную группу, если есть
            const saved = localStorage.getItem('selectedGroup');
            if (saved) {
                this.selectedGroup = saved;
                console.log("Выбрана группа из кэша" + saved);
            }
        },

        async loadGroups() {
            console.log('Загрузка groups');
            this.loadingGroups = true;
            this.error = null;
            try {
                const groups = await repo.fetchGroups();
                this.groups = Array.isArray(groups) ? groups : [];
                if (Array.isArray(groups))
                    console.log("Список groups получен");
            } catch (e) {
                console.error('loadGroups error', e);
                this.error = 'Не удалось загрузить список групп.';
            } finally {
                this.loadingGroups = false;
            }
        },

        selectGroup(group) {
            this.selectedGroup = group;
            console.log("Выбрана группа " + this.selectedGroup);
            try {
                localStorage.setItem('selectedGroup', group);
                console.log("Выбранная группа сохранена в кэш");
            } catch (e) { }
            // Alpine автоматически инициализирует вложенный компонент scheduleComponent(selectedGroup)
        },

        reset() {
            this.selectedGroup = null;
            try { localStorage.removeItem('selectedGroup'); } catch (e) { }
        }
    };
}


export default scheduleApp;