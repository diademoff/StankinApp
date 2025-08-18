import DateUtils from './dateUtils.js';
import SCHEDULE_CONFIG from './config.js';

class ScheduleRepository {
    constructor(apiClient, cache) {
        this.api = apiClient;
        this.cache = cache;
    }

    // Ключ кэша для недели
    cacheKeyForWeek(groupName, startDateApi, endDateApi) {
        return this.cache.buildKey('schedule', groupName, `${startDateApi}_${endDateApi}`);
    }

    // Сначала кэш, иначе API. Возвращает объект: { items: Array, fromCache: bool }
    async fetchWeek(groupName, startDateApi, endDateApi) {
        console.log("fetching week " + groupName +
            ", date=" + startDateApi + ", " + endDateApi);

        const key = this.cacheKeyForWeek(groupName, startDateApi, endDateApi);
        const cached = this.cache.get(key);
        if (cached) {
            console.log("found in cache for date=" + startDateApi + ", " + endDateApi);
            return { items: cached, fromCache: true };
        }
        const items = await this.api.getSchedule(groupName, startDateApi, endDateApi);
        // items может быть []
        this.cache.set(key, items);
        return { items, fromCache: false };
    }

    // Загружает группы (с небольшим кэшем)
    async fetchGroups() {
        const key = this.cache.buildKey('groups');
        const cached = this.cache.get(key, SCHEDULE_CONFIG.CACHE_TTL_MS / 2);
        if (cached) {
            console.log("Группы найдены в кэше");
            return cached;
        }
        const groups = await this.api.getGroups();
        this.cache.set(key, groups);
        return groups;
    }

    // Трансформация: API возвращает элементы, у которых может быть поле dates: [{year,month,day}, ...]
    // Мы хотим получить карту dateStr->array(lesson)
    // Но этот метод не мутирует store — возвращает массив "normalisedLessons"
    normalizeItemsToLessons(items) {
        const out = [];
        for (const it of items || []) {
            const base = {
                groupName: it.groupName,
                subject: it.subject,
                teacher: it.teacher,
                type: it.type,
                subgroup: it.subgroup,
                cabinet: it.cabinet,
                sequencePosition: it.sequencePosition,
                sequenceLength: it.sequenceLength,
                startTime: it.startTime,
                duration: it.duration
            };
            if (!Array.isArray(it.dates) || it.dates.length === 0) {
                // если дата не указана — пропускаем
                continue;
            }
            for (const d of it.dates) {
                const dateObj = DateUtils.parseApiDateObj(d);
                const dateStr = DateUtils.toIsoDate(dateObj);
                out.push(Object.assign({ date: dateStr }, base));
            }
        }
        return out;
    }
}

export default ScheduleRepository;