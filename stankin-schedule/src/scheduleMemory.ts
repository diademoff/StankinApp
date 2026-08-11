import { DateUtils } from './date-utils';
import { Lesson } from './types';

export class ScheduleMemory {
  private map: Record<string, Lesson[]> = {};
  private order: string[] = [];

  setDay(dateStr: string, lessons: Lesson[]): void {
    const sorted = (lessons || []).slice().sort((a, b) => {
      if (a.startTime !== b.startTime) return a.startTime.localeCompare(b.startTime);
      return (a.sequencePosition || 0) - (b.sequencePosition || 0);
    });

    this.map[dateStr] = sorted;
    if (!this.order.includes(dateStr)) {
      this.order.push(dateStr);
      this.order.sort((a, b) => a.localeCompare(b));
    }
  }

  mergeDay(dateStr: string, lessons: Lesson[]): void {
    const existing = (this.map[dateStr] || []).slice();
    const keyset = new Set(existing.map(l => ScheduleMemory.lessonKey(l)));

    for (const l of lessons || []) {
      const k = ScheduleMemory.lessonKey(l);
      if (!keyset.has(k)) {
        existing.push(l);
        keyset.add(k);
      }
    }
    this.setDay(dateStr, existing);
  }

  private static lessonKey(l: Lesson): string {
    return l.id ?? [
      l.subject ?? '',
      l.teacher ?? '',
      l.type ?? '',
      l.cabinet ?? '',
      l.startTime ?? '',
      l.durationMinutes ?? ''
    ].join('|');
  }

  hasDay(dateStr: string): boolean {
    return Object.prototype.hasOwnProperty.call(this.map, dateStr);
  }

  getDay(dateStr: string): Lesson[] {
    return this.map[dateStr] || [];
  }

  ensureDaysRange(startDate: Date, endDate: Date): void {
    const s = new Date(startDate);
    s.setHours(0, 0, 0, 0);
    const e = new Date(endDate);
    e.setHours(0, 0, 0, 0);
    let cur = new Date(s);

    while (cur <= e) {
      const ds = DateUtils.toIsoDate(cur);
      if (!this.hasDay(ds)) this.setDay(ds, []);
      cur.setDate(cur.getDate() + 1);
    }
  }

  asGroupedObject(): Record<string, Lesson[]> {
    const out: Record<string, Lesson[]> = {};
    this.order.sort((a, b) => a.localeCompare(b));
    for (const d of this.order) {
      out[d] = this.map[d] || [];
    }
    return out;
  }

  earliestDate(): string | null {
    return this.order.length === 0 ? null : this.order[0];
  }

  latestDate(): string | null {
    return this.order.length === 0 ? null : this.order[this.order.length - 1];
  }
}