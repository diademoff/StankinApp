import { Lesson } from './types';

// Посуточный снапшот загруженных расписаний в localStorage.
// В отличие от кэша Service Worker, он не зависит от версии/активации SW:
// данные, полученные однажды при интернете, переживают офлайн в любом раскладе.
// Структура записи на subject: { v: 1, days: { "2026-09-01": Lesson[], ... } }
// Пустые дни загруженных недель тоже хранятся (как []), чтобы офлайн не врал
// «Пар нет» для никогда не загружавшихся дней.

const PREFIX = 'sched-snap:';
const MAX_DAYS_PER_SUBJECT = 120;

interface SubjectSnapshot {
  v: 1;
  days: Record<string, Lesson[]>;
}

function keyFor(mode: string, subject: string): string {
  return `${PREFIX}${mode}:${encodeURIComponent(subject)}`;
}

function read(mode: string, subject: string): SubjectSnapshot | null {
  try {
    const raw = localStorage.getItem(keyFor(mode, subject));
    if (!raw) return null;
    const parsed = JSON.parse(raw);
    return parsed && parsed.days ? { v: 1, days: parsed.days } : null;
  } catch {
    return null;
  }
}

function write(mode: string, subject: string, snap: SubjectSnapshot) {
  try {
    localStorage.setItem(keyFor(mode, subject), JSON.stringify(snap));
  } catch {
    // квота исчерпана — урезаем до половины самых свежих дней и пробуем снова
    const dates = Object.keys(snap.days).sort();
    for (const d of dates.slice(0, Math.ceil(dates.length / 2))) delete snap.days[d];
    try {
      localStorage.setItem(keyFor(mode, subject), JSON.stringify(snap));
    } catch {
      /* офлайн-копия недоступна — живём без неё */
    }
  }
}

// сохраняет дни (date → актуальный список занятий недели, пустые тоже)
export function saveDays(mode: string, subject: string, days: Array<[string, Lesson[]]>): void {
  if (days.length === 0) return;
  const snap = read(mode, subject) ?? { v: 1, days: {} };
  for (const [date, lessons] of days) {
    snap.days[date] = lessons;
  }
  const dates = Object.keys(snap.days).sort();
  if (dates.length > MAX_DAYS_PER_SUBJECT) {
    for (const d of dates.slice(0, dates.length - MAX_DAYS_PER_SUBJECT)) delete snap.days[d];
  }
  write(mode, subject, snap);
}

// возвращает данные для диапазона [startIso..endIso] и множество известных дней
export function restoreRange(
  mode: string,
  subject: string,
  startIso: string,
  endIso: string
): { lessons: Lesson[]; knownDates: Set<string> } | null {
  const snap = read(mode, subject);
  if (!snap) return null;

  const lessons: Lesson[] = [];
  const knownDates = new Set<string>();
  for (const [date, dayLessons] of Object.entries(snap.days)) {
    if (date >= startIso && date <= endIso) {
      knownDates.add(date);
      lessons.push(...dayLessons);
    }
  }
  return { lessons, knownDates };
}
