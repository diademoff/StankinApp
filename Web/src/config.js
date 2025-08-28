const SCHEDULE_CONFIG = {
    CACHE_PREFIX: 'scheduleCache:v1',
    CACHE_TTL_MS: 2 * 60 * 60 * 1000, // 2 часа
    DEFAULT_WEEK_DAY_START: 1, // 0 - воскресенье, 1 - понедельник (Россия)
};

export default SCHEDULE_CONFIG;