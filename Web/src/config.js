window.API_BASE_URL = 'http://89.111.131.170:5001';

const SCHEDULE_CONFIG = {
    CACHE_PREFIX: 'scheduleCache:v1',
    CACHE_TTL_MS: 2 * 60 * 60 * 1000, // 2 часа
    DEFAULT_WEEK_DAY_START: 1, // 0 - воскресенье, 1 - понедельник (Россия)
    BASE_URL: window.API_BASE_URL
};

export default SCHEDULE_CONFIG;