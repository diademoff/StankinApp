import { ScheduleRepository } from '../../core/ports/ScheduleRepository';
import { ApiClient } from '../api/ApiClient';
import { LocalStorageCache } from '../cache/LocalStorageCache';
import { Lesson } from '../../shared/types';
import { DateUtils } from '../../shared/date-utils';

export class HttpScheduleRepository implements ScheduleRepository {
  constructor(private api: ApiClient, private cache: LocalStorageCache) {}

  async fetchWeek(group: string, startDate: string, endDate: string): Promise<Lesson[]> {
    const key = this.cache.buildKey('schedule', group, `${startDate}_${endDate}`);
    const cached = this.cache.get(key);
    if (cached) return cached;

    const items = await this.api.getSchedule(group, startDate, endDate);
    const lessons = this.normalizeItemsToLessons(items);
    this.cache.set(key, lessons);
    return lessons;
  }

  private normalizeItemsToLessons(items: any[]): Lesson[] {
    const out: Lesson[] = [];
    for (const it of items) {
      const base = {
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
      if (!Array.isArray(it.dates)) continue;
      for (const d of it.dates) {
        const dateObj = new Date(d.year, d.month - 1, d.day);
        const dateStr = DateUtils.toIsoDate(dateObj);
        out.push({ ...base, date: dateStr });
      }
    }
    return out;
  }
}
