import { LoadScheduleWeekUseCase } from '../../core/use-cases/LoadScheduleWeekUseCase';
import { DateUtils } from '../../shared/date-utils';
import { Lesson } from '../../shared/types';
import { ScheduleMemory } from '../../shared/scheduleMemory';
import Swiper from 'swiper';
import 'swiper/css';

interface TopComment {
  id: number;
  content: string;
  votes: number;
}

interface RatingData {
  id: number;
  avg: number | '-'; // потому что у "Не указан" стоит '-'
  count: number;
  topComments: TopComment[];
}

export function scheduleComponent(
  groupName: string,
  loadScheduleUseCase: LoadScheduleWeekUseCase
) {
  const mem = new ScheduleMemory();

  return {
    groupName,
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

    updateGroupedSchedule() {
      this.groupedSchedule = mem.asGroupedObject();
      this.isEmptySchedule = Object.keys(this.groupedSchedule).every(
        date => this.groupedSchedule[date].length === 0
      );
    },

    ensureWeekIsLoadedInMemory(weekStartDate: Date): boolean {
      if (!weekStartDate) return false;
      const days = DateUtils.rangeDays(weekStartDate, 7);
      for (const d of days) {
        const ds = DateUtils.toIsoDate(d);
        if (!mem.hasDay(ds)) {
          return false;
        }
      }
      return true;
    },

    async loadWeek(weekStartDate: Date, direction: 'top' | 'bottom' | 'initial' = 'bottom') {
      if (!this.groupName || !weekStartDate) {
        this.error = 'Не указана группа или дата';
        return;
      }

      const startApi = DateUtils.formatDateForApi(weekStartDate);
      const endApi = DateUtils.formatDateForApi(DateUtils.addDays(weekStartDate, 6));

      try {
        if (direction === 'top') {
          this.loadingTop = true;
        } else if (direction === 'bottom') {
          this.loadingBottom = true;
        } else {
          this.loading = true;
        }
        this.loadingDir = direction;
        this.error = null;

        const lessons = await loadScheduleUseCase.execute(this.groupName, startApi, endApi);

        const days = DateUtils.rangeDays(weekStartDate, 7);
        for (const d of days) {
          const ds = DateUtils.toIsoDate(d);
          const lessonsForDay = lessons.filter(l => l.date === ds);
          mem.mergeDay(ds, lessonsForDay);
        }

        for (const l of lessons) {
          if (!mem.hasDay(l.date)) mem.setDay(l.date, [l]);
        }

        const weekEnd = DateUtils.addDays(weekStartDate, 6);
        mem.ensureDaysRange(weekStartDate, weekEnd);

        this.updateGroupedSchedule();
        return { lessons };
      } catch (e) {
        console.error('loadWeek error', e);
        this.error = 'Ошибка загрузки расписания.';
        throw e;
      } finally {
        this.loading = false;
        this.loadingTop = false;
        this.loadingBottom = false;
        this.loadingDir = null;
        this.initialLoadDone = true;
      }
    },

    async loadMore(direction: 'top' | 'bottom') {
      if (direction === 'top') {
        if (this.loadingTop) return;
        const earliest = mem.earliestDate();
        let anchorDate = earliest ? new Date(earliest + 'T00:00:00') : DateUtils.startOfWeek(new Date());
        const newWeekStart = DateUtils.startOfWeek(DateUtils.addDays(anchorDate, -7));

        const container = (this as any).$refs?.scheduleContainer;
        let prevScrollHeight = 0, prevScrollTop = 0;
        if (container) {
          prevScrollHeight = container.scrollHeight;
          prevScrollTop = container.scrollTop;
        }

        await this.loadWeek(newWeekStart, 'top');

        if (container) {
          const delta = container.scrollHeight - prevScrollHeight;
          container.scrollTop = prevScrollTop + delta;
        }
      } else if (direction === 'bottom') {
        if (this.loadingBottom) return;
        const latest = mem.latestDate();
        let anchorDate = latest ? new Date(latest + 'T00:00:00') : DateUtils.startOfWeek(new Date());
        const newWeekStart = DateUtils.startOfWeek(DateUtils.addDays(anchorDate, 7));
        await this.loadWeek(newWeekStart, 'bottom');
      }
    },

    async onDateClick(dateStr: string) {
      try {
        const clicked = new Date(dateStr + 'T00:00:00');
        const weekStart = DateUtils.startOfWeek(clicked);

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
          if (attempt < maxAttempts) {
            return tryScroll(attempt + 1);
          }
          return;
        }

        try {
          el.scrollIntoView({ behavior: 'smooth', block: 'start', inline: 'nearest' });
        } catch (e) {
          const top = el.offsetTop - container.offsetTop;
          container.scrollTo({ top, behavior: 'smooth' });
        }
      };

      const clickedDate = new Date(dateStr + 'T00:00:00');
      const weekStart = DateUtils.startOfWeek(clickedDate);
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

    updateDateRanges() {
      const prevStart = DateUtils.addDays(this.weekStart, -7);
      this.dateRanges[0] = this.generateRange(prevStart);
      this.dateRanges[1] = this.generateRange(this.weekStart);
      const nextStart = DateUtils.addDays(this.weekStart, 7);
      this.dateRanges[2] = this.generateRange(nextStart);
    },

    generateRange(start: Date): string[] {
      const arr: string[] = [];
      const d = DateUtils.startOfWeek(start);
      for (let i = 0; i < 7; i++) {
        arr.push(DateUtils.toIsoDate(DateUtils.addDays(d, i)));
      }
      return arr;
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

        await new Promise(r => requestAnimationFrame(r));
        this.setupObservers();

        const now = DateUtils.toIsoDate(new Date());
        await (this as any).$nextTick();
        await this.scrollToDate(now, 5, 200);
      } catch (e) {
        console.error('initial load failed', e);
      }
    },

    formatDate(d: string) { return DateUtils.formatDateHuman(d); },
    formatDateShort(d: string) { return DateUtils.formatDateShort(d); },
    formatTime(t: { hour: number; minute: number }) { return DateUtils.formatTime(t); },
    calculateEndTime: DateUtils.calculateEndTime,

    hasSchedule(dateStr: string) { return mem.hasDay(dateStr); },
    isSelectedDate(dateStr: string) { return dateStr === DateUtils.toIsoDate(new Date()); },

    lessonKey(date: string, l: Lesson) {
      return `${date}-${l.subject}-${l.startTime?.hour ?? '00'}-${l.startTime?.minute ?? '00'}-${l.subgroup ?? ''}`;
    },

    typeBadgeClass(typeRaw: string) {
      const t = (typeRaw || '').toLowerCase();
      if (t.includes('лекц')) return 'bg-blue-50 text-blue-700 ring-blue-200';
      if (t.includes('лаб')) return 'bg-green-50 text-green-700 ring-green-200';
      if (t.includes('сем')) return 'bg-purple-50 text-purple-700 ring-purple-200';
      return 'bg-gray-50 text-gray-700 ring-gray-200';
    },

    formatCabinet(cab?: string) {
      const v = (cab || '').trim();
      return v.length ? v : 'кабинет не указан';
    },

    formatSequence(pos: number, len: number, typeRaw: string) {
      return `${pos} из ${len}`;
    },

    destroy() {
      this.disconnectObservers();
    },

    openDiscussionModal(lesson: Lesson) {
      if (!lesson.teacher) return; // Не открывать окно, если преподаватель не указан
      this.selectedLessonForModal = lesson;
      this.isDiscussionModalOpen = true;
      console.log("Открыт DiscussionModal для ", lesson);
    },

    closeDiscussionModal() {
      this.isDiscussionModalOpen = false;
    },

    getTeacherRating(teacherName: string) {
      return fetch(`/api/teachers/${encodeURIComponent(teacherName)}/ratings`)
        .then(res => res.json())
        .then(data => ({ avg: data.averageScore || 'N/A', count: data.ratingsCount || 0 }))
        .catch(err => {
          console.error('Error fetching rating:', err);
          return { avg: 'N/A', count: 0 };
        });
    },

    getTopComments(teacherName: string) {
      return fetch(`/api/teachers/${encodeURIComponent(teacherName)}/comments?page=1&limit=3`)
        .then(res => res.json())
        .then(data => data.comments.map((c: { id: number; content: string; votes: number; }) => ({
          id: c.id as number,
          content: c.content as string,
          votes: c.votes as number
        })) || [])
        .catch(err => {
          console.error('Error fetching comments:', err);
          return [];
        });
    }
  };
}