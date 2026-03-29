import { GroupRepository } from '../../core/ports/GroupRepository';
import { SCHEDULE_CONFIG } from '../../shared/config';
import { ApiClient } from '../api/ApiClient';
import { LocalStorageCache } from '../cache/LocalStorageCache';

export class HttpGroupRepository implements GroupRepository {
  constructor(private api: ApiClient, private cache: LocalStorageCache) {}

  async fetchGroups(): Promise<string[]> {
    const key = this.cache.buildKey('groups');
    const cached = this.cache.get(key, SCHEDULE_CONFIG.CACHE_TTL_MS / 2);
    if (cached) return cached;

    const groups = await this.api.getGroups();
    this.cache.set(key, groups);
    return groups;
  }
}