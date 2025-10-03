import { LoadScheduleWeekUseCase } from '../../core/use-cases/LoadScheduleWeekUseCase';
import { DateUtils } from '../../shared/date-utils';
import { Lesson } from '../../shared/types';

export function scheduleComponent(
  groupName: string,
  loadScheduleUseCase: LoadScheduleWeekUseCase
) {
  return {
    groupName,
    loading: false,
    error: null as string | null,
    groupedSchedule: {} as Record<string, Lesson[]>,

    async init() {
      const start = DateUtils.startOfWeek(new Date());
      const end = DateUtils.addDays(start, 6);
      try {
        const lessons = await loadScheduleUseCase.execute(
          this.groupName,
          DateUtils.formatDateForApi(start),
          DateUtils.formatDateForApi(end)
        );
        this.groupedSchedule = this.groupLessonsByDate(lessons);
      } catch (e) {
        this.error = 'Ошибка загрузки расписания';
        console.error(e);
      }
    },

    groupLessonsByDate(lessons: Lesson[]): Record<string, Lesson[]> {
      const map: Record<string, Lesson[]> = {};
      for (const l of lessons) {
        if (!map[l.date]) map[l.date] = [];
        map[l.date].push(l);
      }
      return map;
    },

    formatDate(d: string) { return DateUtils.formatDateHuman(d); },
    formatDateShort(d: string) { return DateUtils.formatDateShort(d); },
    formatTime(t: { hour: number; minute: number }) { return DateUtils.formatTime(t); },
    calculateEndTime: DateUtils.calculateEndTime
  };
}
