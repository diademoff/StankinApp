import SCHEDULE_CONFIG from './config.js';

class LocalCache {
    constructor(prefix = SCHEDULE_CONFIG.CACHE_PREFIX) {
        this.prefix = prefix;
    }

    buildKey(...parts) {
        return [this.prefix, ...parts].join(':');
    }

    set(key, value) {
        const payload = {
            ts: Date.now(),
            value
        };
        try {
            localStorage.setItem(key, JSON.stringify(payload));
        } catch (e) {
            // localStorage может быть переполнен — игнорируем
            console.warn('Cache set failed', e);
        }
    }

    get(key, ttl = SCHEDULE_CONFIG.CACHE_TTL_MS) {
        try {
            const raw = localStorage.getItem(key);
            if (!raw) return null;
            const parsed = JSON.parse(raw);
            if (!parsed || typeof parsed.ts !== 'number') return null;
            if ((Date.now() - parsed.ts) > ttl) {
                // устарело
                localStorage.removeItem(key);
                return null;
            }
            return parsed.value;
        } catch (e) {
            console.warn('Cache get failed', e);
            return null;
        }
    }

    remove(key) {
        try {
            localStorage.removeItem(key);
        } catch (e) { }
    }
}


export default LocalCache;