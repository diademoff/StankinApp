import { SCHEDULE_CONFIG } from './config';

export class DateUtils {
  static formatDateForApi(d: Date): string {
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  }

  static toIsoDate(d: Date): string {
    return DateUtils.formatDateForApi(d);
  }

  static startOfWeek(date: Date, weekStart = SCHEDULE_CONFIG.DEFAULT_WEEK_DAY_START): Date {
    const d = new Date(date.getFullYear(), date.getMonth(), date.getDate());
    const day = d.getDay();
    const diff = (day - weekStart + 7) % 7;
    d.setDate(d.getDate() - diff);
    d.setHours(0, 0, 0, 0);
    return d;
  }

  static addDays(date: Date, n: number): Date {
    const d = new Date(date);
    d.setDate(d.getDate() + n);
    return d;
  }

  static rangeDays(start: Date, length = 7): Date[] {
    const out: Date[] = [];
    const d = new Date(start);
    for (let i = 0; i < length; i++) {
      out.push(new Date(d.getFullYear(), d.getMonth(), d.getDate()));
      d.setDate(d.getDate() + 1);
    }
    return out;
  }

  static formatDateHuman(d: string): string {
    // Разбираем "2026-03-29" вручную, чтобы не зависеть от часового пояса
    const [year, month, day] = d.split('-').map(Number);
    const date = new Date(year, month - 1, day);
    const dateStr = date.toLocaleDateString('ru-RU', {
      weekday: 'long',
      day: 'numeric',
      month: 'long'
    });
    return dateStr.charAt(0).toUpperCase() + dateStr.slice(1);
  }

  static formatDateShort(d: string): string {
    const [year, month, day] = d.split('-').map(Number);
    const x = new Date(year, month - 1, day);
    const m = ['янв.', 'фев.', 'мар.', 'апр.', 'май', 'июн.', 'июл.', 'авг.', 'сен.', 'окт.', 'ноя.', 'дек.'];
    return `${x.getDate()} ${m[x.getMonth()]}`;
  }
}