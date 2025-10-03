import { SCHEDULE_CONFIG } from '../../shared/config';

export class LocalStorageCache {
  constructor(private prefix = SCHEDULE_CONFIG.CACHE_PREFIX) {}

  buildKey(...parts: string[]): string {
    return [this.prefix, ...parts].join(':');
  }

  set(key: string, value: any): void {
    const payload = { ts: Date.now(), value };
    try {
      localStorage.setItem(key, JSON.stringify(payload));
    } catch (e) {
      console.warn('Cache set failed', e);
    }
  }

  get(key: string, ttl = SCHEDULE_CONFIG.CACHE_TTL_MS): any | null {
    try {
      const raw = localStorage.getItem(key);
      if (!raw) return null;
      const parsed = JSON.parse(raw);
      if (!parsed || typeof parsed.ts !== 'number') return null;
      if (Date.now() - parsed.ts > ttl) {
        localStorage.removeItem(key);
        return null;
      }
      return parsed.value;
    } catch (e) {
      console.warn('Cache get failed', e);
      return null;
    }
  }
}
