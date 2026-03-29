import { ScheduleRepository } from '../../core/ports/ScheduleRepository';
import { ApiClient } from '../api/ApiClient';
import { LocalStorageCache } from '../cache/LocalStorageCache';
import { Lesson } from '../../shared/types';

export class HttpScheduleRepository implements ScheduleRepository {
  constructor(private api: ApiClient, private cache: LocalStorageCache) {}

  async fetchWeek(group: string, startDate: string, endDate: string): Promise<Lesson[]> {
    const key = this.cache.buildKey('schedule', group, `${startDate}_${endDate}`);
    const cached = this.cache.get(key);
    if (cached) return cached;

    const items = await this.api.getSchedule(group, startDate, endDate);
    const lessons = this.mapToLessons(items);
    this.cache.set(key, lessons);
    return lessons;
  }

  private mapToLessons(items: any[]): Lesson[] {
    return (items || []).map(it => ({
      id:               it.id,
      date:             it.date,             // "2026-03-29"
      startTime:        it.startTime,        // "08:30"
      endTime:          it.endTime,          // "10:00"
      durationMinutes:  it.durationMinutes,
      groupName:        it.groupName,
      subject:          it.subject,
      teacher:          it.teacher || undefined,
      type:             it.type,             // "Лекция" | "Семинар" | ...
      subgroup:         it.subgroup || undefined,
      cabinet:          it.cabinet || undefined,
      sequencePosition: it.sequencePosition,
      sequenceLength:   it.sequenceLength,
    }));
  }
}