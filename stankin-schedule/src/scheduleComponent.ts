import { ApiClient } from './ApiClient';
import { DateUtils } from './date-utils';
import { Lesson } from './types';
import { ScheduleMemory } from './scheduleMemory';
import * as scheduleStore from './scheduleStore';
import Swiper from 'swiper';
import 'swiper/css';

export function scheduleComponent(
  subjectName: string,
  viewMode: 'group' | 'teacher',
  api: ApiClient
) {
  const mem = new ScheduleMemory();

  return {
    subjectName,
    viewMode,
    loading: false,
    loadingDir: null as 'top' | 'bottom' | 'initial' | null,
    error: null as string | null,
    weekStart: DateUtils.startOfWeek(new Date()),
    dateRanges: [[], [], []] as string[][],
    swiperInstance: null as Swiper | null,
    isEmptySchedule: true,
    groupedSchedule: {} as Record<string, Lesson[]>,
    loadingTop: false,
    loadingBottom: false,
    initialLoadDone: false,
    observerTop: null as IntersectionObserver | null,
    observerBottom: null as IntersectionObserver | null,
    updating: false,
    isDiscussionModalOpen: false,
    selectedLessonForModal: null as Lesson | null,
    relatedLessons: [] as Lesson[],
    loadingRelated: false,
    onScheduleUpdate: null as ((e: MessageEvent) => void) | null,

    updateGroupedSchedule() {
      const raw = mem.asGroupedObject();

      if (this.viewMode === 'teacher') {
        const merged: Record<string, Lesson[]> = {};
        for (const [date, lessons] of Object.entries(raw)) {
          const map = new Map<string, Lesson>();
          for (const l of lessons) {
            const key = l.startTime + '|' + l.subject + '|' + (l.cabinet ?? '');
            if (map.has(key)) {
              const existing = map.get(key)!;
              if (existing.groupName && l.groupName && !existing.groupName.includes(l.groupName)) {
                const groups = existing.groupName.split(', ');
                groups.push(l.groupName);
                groups.sort();
                existing.groupName = groups.join(', ');
              }
            } else {
              map.set(key, { ...l });
            }
          }
          merged[date] = Array.from(map.values());
        }
        this.groupedSchedule = merged;
      } else {
        this.groupedSchedule = raw;
      }

      this.isEmptySchedule = Object.keys(this.groupedSchedule).every(
        date => this.groupedSchedule[date].length === 0
      );
    },

    ensureWeekIsLoadedInMemory(weekStartDate: Date): boolean {
      if (!weekStartDate) return false;
      const days = DateUtils.rangeDays(weekStartDate, 7);
      for (const d of days) {
        const ds = DateUtils.toIsoDate(d);
        if (!mem.hasDay(ds)) return false;
      }
      return true;
    },

    async loadWeek(weekStartDate: Date, direction: 'top' | 'bottom' | 'initial' = 'bottom') {
      if (!this.subjectName || !weekStartDate) {
        this.error = 'Не указана группа/преподаватель или дата';
        return;
      }

      const startApi = DateUtils.toIsoDate(weekStartDate);
      const endApi   = DateUtils.toIsoDate(DateUtils.addDays(weekStartDate, 6));

      try {
        if (direction === 'top')         this.loadingTop    = true;
        else if (direction === 'bottom') this.loadingBottom = true;
        else                             this.loading       = true;

        this.loadingDir = direction;
        this.error = null;

        let lessons: Lesson[];
        let items: any[];
        if (this.viewMode === 'teacher') {
          items = await api.getTeacherSchedule(this.subjectName, startApi, endApi);
        } else {
          items = await api.getSchedule(this.subjectName, startApi, endApi);
        }
        lessons = (items ?? []) as Lesson[];

        this.ingestWeek(weekStartDate, lessons);
        return { lessons };
      } catch (e) {
        // сеть/api недоступны — пытаемся показать ранее загруженные дни из снапшота
        const restored = this.tryRestoreOffline(startApi, endApi, weekStartDate);
        if (restored) return { lessons: restored };
        console.error('loadWeek error', e);
        const networkError = (e as any)?.network === true || navigator.onLine === false;
        this.error = networkError
          ? 'Нет соединения — доступно только ранее загруженное расписание'
          : 'Ошибка загрузки расписания.';
        throw e;
      } finally {
        this.loading       = false;
        this.loadingTop    = false;
        this.loadingBottom = false;
        this.loadingDir    = null;
        this.initialLoadDone = true;
      }
    },

    // офлайн-резерв из scheduleStore: показываем только реально загруженные дни
    tryRestoreOffline(startApi: string, endApi: string, weekStartDate: Date): Lesson[] | null {
      try {
        const snap = scheduleStore.restoreRange(this.viewMode, this.subjectName, startApi, endApi);
        if (!snap || snap.knownDates.size === 0) return null;
        this.ingestWeek(weekStartDate, snap.lessons, { persist: false, knownDays: snap.knownDates });
        return snap.lessons;
      } catch (err) {
        console.error('tryRestoreOffline error', err);
        return null;
      }
    },

    ingestWeek(weekStartDate: Date, lessons: Lesson[], opts: { persist?: boolean; knownDays?: Set<string> | null } = {}) {
      const persist = opts.persist ?? true;
      const knownDays = opts.knownDays ?? null;

      const days = DateUtils.rangeDays(weekStartDate, 7);
      const saved: Array<[string, Lesson[]]> = [];
      for (const d of days) {
        const ds = DateUtils.toIsoDate(d);
        if (knownDays && !knownDays.has(ds)) continue; // офлайн: неизвестные дни не трогаем
        const lessonsForDay = lessons.filter(l => l.date === ds);
        mem.mergeDay(ds, lessonsForDay);
        saved.push([ds, mem.getDay(ds)]);
      }

      for (const l of lessons) {
        if (!mem.hasDay(l.date)) mem.setDay(l.date, [l]);
      }

      if (!knownDays) {
        const weekEnd = DateUtils.addDays(weekStartDate, 6);
        mem.ensureDaysRange(weekStartDate, weekEnd);
      }

      if (persist && saved.length > 0) scheduleStore.saveDays(this.viewMode, this.subjectName, saved);

      this.updateGroupedSchedule();
    },

    async loadMore(direction: 'top' | 'bottom') {
      if (direction === 'top') {
        if (this.loadingTop) return;
        const earliest   = mem.earliestDate();
        const anchorDate = earliest ? new Date(earliest + 'T00:00:00') : DateUtils.startOfWeek(new Date());
        const newWeekStart = DateUtils.startOfWeek(DateUtils.addDays(anchorDate, -7));

        const container = (this as any).$refs?.scheduleContainer;
        let prevScrollHeight = 0, prevScrollTop = 0;
        if (container) {
          prevScrollHeight = container.scrollHeight;
          prevScrollTop    = container.scrollTop;
        }

        await this.loadWeek(newWeekStart, 'top');

        if (container) {
          const delta = container.scrollHeight - prevScrollHeight;
          container.scrollTop = prevScrollTop + delta;
        }
      } else if (direction === 'bottom') {
        if (this.loadingBottom) return;
        const latest     = mem.latestDate();
        const anchorDate = latest ? new Date(latest + 'T00:00:00') : DateUtils.startOfWeek(new Date());
        const newWeekStart = DateUtils.startOfWeek(DateUtils.addDays(anchorDate, 7));
        await this.loadWeek(newWeekStart, 'bottom');
      }
    },

    async onDateClick(dateStr: string) {
      try {
        const clicked    = new Date(dateStr + 'T00:00:00');
        const weekStart  = DateUtils.startOfWeek(clicked);

        if (!this.ensureWeekIsLoadedInMemory(weekStart)) {
          await this.loadWeek(weekStart, 'initial');
        }

        await this.scrollToDate(dateStr, 5, 200);
        this.updateGroupedSchedule();
      } catch (e) {
        console.error('onDateClick error', e);
      }
    },

    async scrollToDate(dateStr: string, maxAttempts = 5, attemptDelay = 200) {
      const container = (this as any).$refs?.scheduleContainer;
      if (!container) return;

      const tryScroll = async (attempt = 1): Promise<void> => {
        await new Promise(resolve => setTimeout(resolve, attemptDelay));
        const el = container.querySelector(`#date-${dateStr}`);
        if (!el) {
          if (attempt < maxAttempts) return tryScroll(attempt + 1);
          return;
        }
        try {
          el.scrollIntoView({ behavior: 'smooth', block: 'start', inline: 'nearest' });
        } catch {
          const offset = el.getBoundingClientRect().top - container.getBoundingClientRect().top + container.scrollTop - 8;
          container.scrollTo({ top: offset, behavior: 'smooth' });
        }
      };

      const clickedDate = new Date(dateStr + 'T00:00:00');
      const weekStart   = DateUtils.startOfWeek(clickedDate);
      if (!this.ensureWeekIsLoadedInMemory(weekStart)) {
        await this.loadWeek(weekStart, 'initial');
        this.updateGroupedSchedule();
      }

      await tryScroll();
    },

    setupObservers() {
      const container = (this as any).$refs.scheduleContainer;
      if (!container || !(this as any).$refs.loadMoreTop || !(this as any).$refs.loadMoreBottom) return;

      this.observerTop = new IntersectionObserver(async (entries) => {
        for (const e of entries) {
          if (e.isIntersecting && this.initialLoadDone && !this.loadingTop) {
            await this.loadMore('top');
          }
        }
      }, { root: container, rootMargin: '150px 0px', threshold: 0.01 });

      this.observerBottom = new IntersectionObserver(async (entries) => {
        for (const e of entries) {
          if (e.isIntersecting && this.initialLoadDone && !this.loadingBottom) {
            await this.loadMore('bottom');
          }
        }
      }, { root: container, rootMargin: '150px 0px', threshold: 0.01 });

      this.observerTop.observe((this as any).$refs.loadMoreTop);
      this.observerBottom.observe((this as any).$refs.loadMoreBottom);
    },

    disconnectObservers() {
      this.observerTop?.disconnect();
      this.observerBottom?.disconnect();
      this.observerTop = null;
      this.observerBottom = null;
    },

    // свежая неделя от SW после фоновой актуализации — перерисовка без рефетча
    applyScheduleUpdate(e: MessageEvent) {
      const data = (e as any).data;
      if (!data || data.type !== 'SCHEDULE_UPDATED') return;
      try {
        const url = new URL(data.url, location.origin);
        const params = url.searchParams;
        if (params.has('subject')) return; // by-subject — не в ленту
        if (this.viewMode === 'teacher') {
          if (params.get('teacherName') !== this.subjectName) return;
        } else {
          if (params.get('groupName') !== this.subjectName) return;
        }
        const startApi = params.get('startDate');
        if (!startApi || !mem.hasDay(startApi)) return; // неделя уже открыта в памяти

        const parsed = JSON.parse(data.data);
        const items = Array.isArray(parsed) ? parsed : (parsed?.items ?? []);
        const weekStart = new Date(startApi + 'T00:00:00');
        this.ingestWeek(weekStart, items as Lesson[]);
      } catch (err) {
        console.error('applyScheduleUpdate error', err);
      }
    },

    updateDateRanges() {
      const prevStart = DateUtils.addDays(this.weekStart, -7);
      this.dateRanges[0] = DateUtils.rangeDays(prevStart, 7).map(DateUtils.toIsoDate);
      this.dateRanges[1] = DateUtils.rangeDays(this.weekStart, 7).map(DateUtils.toIsoDate);
      const nextStart = DateUtils.addDays(this.weekStart, 7);
      this.dateRanges[2] = DateUtils.rangeDays(nextStart, 7).map(DateUtils.toIsoDate);
    },

    async init() {
      this.updateDateRanges();

      (this as any).$nextTick(() => {
        const self = this;
        this.swiperInstance = new Swiper((this as any).$refs.swiper, {
          initialSlide: 1,
          slidesPerView: 1,
          speed: 400,
          observeParents: true,
          runCallbacksOnInit: false,
          on: {
            slideChange: function (this: Swiper) {
              const swiper = this;
              const activeIndex = swiper.activeIndex;

              if (self.updating) return;
              self.updating = true;

              if (activeIndex > 1) {
                self.weekStart = DateUtils.addDays(self.weekStart, 7);
              } else if (activeIndex < 1) {
                self.weekStart = DateUtils.addDays(self.weekStart, -7);
              } else {
                self.updating = false;
                return;
              }

              (self as any).$nextTick(() => {
                setTimeout(() => {
                  if (self.updating) {
                    swiper.slideTo(1, 0);
                    self.updating = false;
                    self.updateDateRanges();
                  }
                }, swiper.params.speed);
              });
            }
          },
        });
      });

      this.groupedSchedule = {};
      this.isEmptySchedule = true;
      this.weekStart = DateUtils.startOfWeek(new Date());

      try {
        await this.loadWeek(this.weekStart, 'initial');
        this.updateGroupedSchedule();

        // SW присылает свежую неделю (SCHEDULE_UPDATED) после фоновой актуализации
        const self = this;
        this.onScheduleUpdate = (e) => self.applyScheduleUpdate(e);
        window.addEventListener('message', this.onScheduleUpdate);
        await (this as any).$nextTick();

        const container = (this as any).$refs.scheduleContainer;
        if (container) {
          const todayStr     = DateUtils.toIsoDate(new Date());
          const todayElement = container.querySelector(`#date-${todayStr}`) as HTMLElement;
          if (todayElement) {
            const offset = todayElement.getBoundingClientRect().top
              - container.getBoundingClientRect().top
              + container.scrollTop
              - 8;
            container.scrollTop = offset;
          }
        }

        this.setupObservers();
      } catch (e) {
        console.error('initial load failed', e);
      }
    },

    formatDate(d: string)      { return DateUtils.formatDateHuman(d); },
    formatDateShort(d: string) { return DateUtils.formatDateShort(d); },
    hasSchedule(dateStr: string)  { return mem.hasDay(dateStr); },
    isSelectedDate(dateStr: string) { return dateStr === DateUtils.toIsoDate(new Date()); },

    typeCategory(typeRaw: string) {
      const t = (typeRaw || '').toLowerCase();
      if (t.includes('лекц')) return 'lecture';
      if (t.includes('лаб'))  return 'lab';
      if (t.includes('сем'))  return 'seminar';
      return null;
    },

    getLessonIndicators(date: string) {
      const lessons = this.groupedSchedule[date] ?? [];
      const counts = { lecture: 0, seminar: 0, lab: 0 };
      for (const l of lessons) {
        const cat = this.typeCategory(l.type);
        if (cat) counts[cat]++;
      }
      const indicators: { type: string; label: string; count: number }[] = [];
      if (counts.lecture) indicators.push({ type: 'lecture', label: 'Лекции', count: counts.lecture });
      if (counts.seminar) indicators.push({ type: 'seminar', label: 'Семинары', count: counts.seminar });
      if (counts.lab) indicators.push({ type: 'lab', label: 'Лабораторные', count: counts.lab });
      return indicators;
    },

    indicatorColor(type: string) {
      return { lecture: 'bg-blue-500', seminar: 'bg-purple-500', lab: 'bg-green-500' }[type] ?? 'bg-gray-400';
    },

    dateAriaLabel(date: string) {
      const parts = this.getLessonIndicators(date).map(i => `${i.label}: ${i.count}`);
      const base = this.formatDate(date);
      return parts.length ? `${base}. ${parts.join(', ')}.` : base;
    },

    lessonKey(date: string, l: Lesson) {
      return l.id ?? `${date}-${l.subject}-${l.startTime}-${l.subgroup ?? ''}`;
    },

    typeBadgeClass(typeRaw: string) {
      const soft = {
        lecture: 'bg-blue-50 text-blue-700 ring-blue-200',
        seminar: 'bg-purple-50 text-purple-700 ring-purple-200',
        lab: 'bg-green-50 text-green-700 ring-green-200',
      };
      return soft[this.typeCategory(typeRaw) ?? ''] ?? 'bg-gray-50 text-gray-700 ring-gray-200';
    },

    typeDotBg(typeRaw: string) {
      return this.indicatorColor(this.typeCategory(typeRaw) ?? '');
    },

    formatCabinet(cab?: string) {
      const v = (cab || '').trim();
      return v.length ? v : 'кабинет не указан';
    },

    formatSequence(pos: number, len: number) {
      return `${pos} из ${len}`;
    },

    destroy() {
      this.disconnectObservers();
      if (this.onScheduleUpdate) {
        window.removeEventListener('message', this.onScheduleUpdate);
        this.onScheduleUpdate = null;
      }
    },

    openDiscussionModal(lesson: Lesson) {
      this.selectedLessonForModal = lesson;
      this.isDiscussionModalOpen = true;
      this.loadRelatedLessons(lesson);
    },

    closeDiscussionModal() {
      this.isDiscussionModalOpen = false;
      this.relatedLessons = [];
    },

    async loadRelatedLessons(lesson: Lesson) {
      if (!lesson.subject || !lesson.teacher || !lesson.groupName) return;
      this.loadingRelated = true;
      try {
        const items = await api.getScheduleBySubject(lesson.subject, lesson.teacher, lesson.groupName);
        this.relatedLessons = (items ?? []) as Lesson[];
      } catch (e) {
        console.error('loadRelatedLessons error', e);
      } finally {
        this.loadingRelated = false;
      }
    },

    navigateToLessonDate(dateStr: string) {
      this.closeDiscussionModal();
      this.scrollToDate(dateStr);
    },
  };
}
