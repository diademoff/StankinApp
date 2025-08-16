import ApiClient from './apiClient.js';
import LocalCache from './cache.js';
import ScheduleRepository from './scheduleRepository.js';
import ScheduleMemory from './scheduleMemory.js';
import DateUtils from './dateUtils.js';
import SCHEDULE_CONFIG from './config.js';

/* ==========================
   Alpine component для расписания конкретной группы
   x-data="scheduleComponent(selectedGroup)"
   ========================== */
export function scheduleComponent(groupNameInitial) {
    // Создаём объекты низкого уровня
    const api = new ApiClient(window.API_BASE_URL || '');
    const cache = new LocalCache(SCHEDULE_CONFIG.CACHE_PREFIX);
    const repo = new ScheduleRepository(api, cache);
    const mem = new ScheduleMemory();

    console.log("Вызов создания объекта scheduleComponent для " + groupNameInitial);

    // Возвращаемый объект содержит все свойства напрямую
    const apiSurface = {
        groupName: groupNameInitial,
        loading: false,
        loadingDir: null,
        error: null,
        weekStart: DateUtils.startOfWeek(new Date()),
        dateRanges: [[], [], []],  // Новое свойство для трёх недель
        swiperInstance: null,
        isEmptySchedule: true,
        groupedSchedule: {},
        loadingTop: false,
        loadingBottom: false,
        initialLoadDone: false,
        observerTop: null,
        observerBottom: null,

        // Вспомогательные функции внутри компонента
        updateDateRange() {
            const arr = [];
            const d = DateUtils.startOfWeek(this.weekStart);
            for (let i = 0; i < 7; i++) {
                arr.push(DateUtils.toIsoDate(DateUtils.addDays(d, i)));
            }
            this.dateRange = arr;
        },

        updateGroupedSchedule() {
            this.groupedSchedule = mem.asGroupedObject();

            // Проверка на пустое расписание
            this.isEmptySchedule = Object.keys(this.groupedSchedule).every(
                date => this.groupedSchedule[date].length === 0
            );
        },

        ensureWeekIsLoadedInMemory(weekStartDate) {
            if (!weekStartDate) return false;
            // Проверяем: если уже есть все дни этой недели — считаем что загружено.
            const days = DateUtils.rangeDays(weekStartDate, 7);
            for (const d of days) {
                const ds = DateUtils.toIsoDate(d);
                if (!mem.hasDay(ds)) {
                    return false;
                }
            }
            return true;
        },

        // Загрузка недели: weekStartDate — Date (понедельник)
        async loadWeek(weekStartDate, direction = 'bottom') {
            if (!this.groupName || !weekStartDate) {
                this.error = 'Не указана группа или дата';
                return;
            }

            console.log("Подгрузка недели " + weekStartDate);
            const startApi = DateUtils.formatDateForApi(weekStartDate);
            const endApi = DateUtils.formatDateForApi(DateUtils.addDays(weekStartDate, 6));
            const cacheKey = repo.cacheKeyForWeek(this.groupName, startApi, endApi);

            // Если уже загружена в память и кэш не просрочен — можно избежать fetch.
            const cached = cache.get(cacheKey);
            if (cached && this.ensureWeekIsLoadedInMemory(weekStartDate)) {
                return { fromCache: true, items: cached };
            }

            try {
                if (direction === 'top') {
                    this.loadingTop = true;
                    console.log("loadingTop = " + this.loadingTop);
                } else if (direction === 'bottom') {
                    this.loadingBottom = true;
                    console.log("loadingBottom = " + this.loadingBottom);
                } else {
                    this.loading = true;
                    console.log("loading = " + this.loading);
                }
                this.loadingDir = direction;
                this.error = null;

                const { items, fromCache } = await repo.fetchWeek(this.groupName, startApi, endApi);
                // Заполняем память: для каждой даты недели гарантируем запись (даже если пустая)
                // Сначала трансформируем элементы в уроки по датам
                const lessons = repo.normalizeItemsToLessons(items);
                // Для каждой дня недели — соберём соответствующие уроки
                const days = DateUtils.rangeDays(weekStartDate, 7);
                for (const d of days) {
                    const ds = DateUtils.toIsoDate(d);
                    const lessonsForDay = lessons.filter(l => l.date === ds).map(l => {
                        // убираем временное поле date — оставляем только полезные поля (но оставим date для удобства)
                        return Object.assign({}, l);
                    });
                    // merge чтобы не потерять существующие (возможны частичные перекрытия)
                    mem.mergeDay(ds, lessonsForDay);
                }
                // Если API вернул уроки с датами за пределами недели (маловероятно) — тоже добавляем их
                for (const l of lessons) {
                    if (!mem.hasDay(l.date)) mem.setDay(l.date, [l]);
                }

                // Обеспечим пустые дни в диапазоне
                const weekEnd = DateUtils.addDays(weekStartDate, 6);
                mem.ensureDaysRange(weekStartDate, weekEnd);

                this.updateGroupedSchedule();
                return { items, fromCache };
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
                console.log("initialLoadDone");
            }
        },

        // Вспомог: загрузить соседнюю неделю вверх/вниз относительно уже загруженного диапазона
        async loadMore(direction) {
            console.log("Подгрузка расписания " + direction);

            // direction: 'top' or 'bottom'
            if (direction === 'top') {
                if (this.loadingTop) return;
                // определим самую раннюю дату в памяти
                const earliest = mem.earliestDate();
                let anchorDate = earliest ? new Date(earliest + 'T00:00:00') : DateUtils.startOfWeek(new Date());
                // хотим загрузить неделю перед текущей earliest
                const newWeekStart = DateUtils.startOfWeek(DateUtils.addDays(anchorDate, -7));
                // сохраним позицию скролла для компенсации
                const container = this.$refs?.scheduleContainer;
                let prevScrollHeight = 0, prevScrollTop = 0;
                if (container) {
                    prevScrollHeight = container.scrollHeight;
                    prevScrollTop = container.scrollTop;
                }
                await this.loadWeek(newWeekStart, 'top');
                // компенсируем скролл так, чтобы пользователь не "провалился" вниз
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
                // no special scroll compensation necessary for bottom append
            }
        },

        // Клик по дате в карусели: подгрузить неделю с этой датой (если ещё не загружена / устарела), затем скроллить к ней
        async onDateClick(dateStr) {
            try {
                // dateStr = 'YYYY-MM-DD'
                const clicked = new Date(dateStr + 'T00:00:00');
                const weekStart = DateUtils.startOfWeek(clicked);
                // Загружаем неделю (если не загружена)
                if (!this.ensureWeekIsLoadedInMemory(weekStart)) {
                    await this.loadWeek(weekStart, 'initial');
                }
                // Обновлённый groupedSchedule готов — плавно скроллим
                await this.scrollToDate(dateStr);
            } catch (e) {
                console.error('onDateClick error', e);
            }
        },

        // Скролл к указанной дате
        async scrollToDate(dateStr) {
            // Ждём ререндер (микрозадача)
            await new Promise(r => requestAnimationFrame(r));
            const container = this.$refs?.scheduleContainer;
            if (!container) return;
            const el = container.querySelector(`#date-${dateStr}`);
            if (!el) {
                // возможно дата ещё не добавлена — ничего не делаем
                return;
            }
            // плавный скролл внутри контейнера
            // Используем scrollIntoView с опцией — внутри контейнера должна прокручиваться только контейнер.
            try {
                el.scrollIntoView({ behavior: 'smooth', block: 'start', inline: 'nearest' });
            } catch (e) {
                // fallback
                const top = el.offsetTop - container.offsetTop;
                container.scrollTo({ top, behavior: 'smooth' });
            }
        },

        // Инициализация intersection observers для бесконечной прокрутки
        setupObservers() {
            const container = this.$refs.scheduleContainer;
            if (!container || !this.$refs.loadMoreTop || !this.$refs.loadMoreBottom) return;

            // Верхняя загрузка: root = container, наблюдаем за .loadMoreTop
            this.observerTop = new IntersectionObserver(async (entries) => {
                for (const e of entries) {
                    if (e.isIntersecting) {
                        // Не запускаем top-загрузку пока не сделали initialLoad
                        if (!this.initialLoadDone) continue;
                        if (!this.loadingTop) {
                            await this.loadMore('top');
                        }
                    }
                }
            }, { root: container, rootMargin: '150px 0px', threshold: 0.01 });

            this.observerBottom = new IntersectionObserver(async (entries) => {
                for (const e of entries) {
                    if (e.isIntersecting) {
                        if (!this.initialLoadDone) continue;
                        if (!this.loadingBottom) {
                            await this.loadMore('bottom');
                        }
                    }
                }
            }, { root: container, rootMargin: '150px 0px', threshold: 0.01 });

            this.observerTop.observe(this.$refs.loadMoreTop);
            this.observerBottom.observe(this.$refs.loadMoreBottom);
        },

        // Очистка наблюдателей
        disconnectObservers() {
            try {
                this.observerTop?.disconnect();
                this.observerBottom?.disconnect();
            } catch (e) { }
            this.observerTop = null;
            this.observerBottom = null;
        },

        // утилиты для шаблона
        formatDate(d) {
            return DateUtils.formatDateHuman(d);
        },
        formatDateShort(d) {
            return DateUtils.formatDateShort(d);
        },
        formatDateForApi(d) {
            // d — Date или строка
            const D = new Date(d);
            return DateUtils.formatDateForApi(D);
        },
        formatTime(t) {
            return DateUtils.formatTime(t);
        },
        calculateEndTime(startTime, duration) {
            return DateUtils.calculateEndTime(startTime, duration);
        },

        hasSchedule(dateStr) {
            return mem.hasDay(dateStr);
        },
        isSelectedDate(dateStr) {
            return dateStr === DateUtils.toIsoDate(new Date());
        },
        prevWeek() {
            if (this.swiperInstance) {
                this.swiperInstance.slidePrev();
            }
        },
        nextWeek() {
            if (this.swiperInstance) {
                this.swiperInstance.slideNext();
            }
        },
        updateDateRanges() {
            const prevStart = DateUtils.addDays(this.weekStart, -7);
            this.dateRanges[0] = this.generateRange(prevStart);

            this.dateRanges[1] = this.generateRange(this.weekStart);

            const nextStart = DateUtils.addDays(this.weekStart, 7);
            this.dateRanges[2] = this.generateRange(nextStart);
        },
        generateRange(start) {
            const arr = [];
            const d = DateUtils.startOfWeek(start);
            for (let i = 0; i < 7; i++) {
                arr.push(DateUtils.toIsoDate(DateUtils.addDays(d, i)));
            }
            return arr;
        },

        // onDateClick должен:
        //  - подгрузить неделю с этой датой (через API/кэш)
        //  - добавить в список (mem)
        //  - проскроллить к дате
        async onDateClick(dateStr) {
            try {
                // dateStr = 'YYYY-MM-DD'
                const clicked = new Date(dateStr + 'T00:00:00');
                const weekStart = DateUtils.startOfWeek(clicked);
                // Загружаем неделю (если не загружена)
                if (!this.ensureWeekIsLoadedInMemory(weekStart)) {
                    await this.loadWeek(weekStart, 'initial');
                }
                // Обновлённый groupedSchedule готов — плавно скроллим
                await this.scrollToDate(dateStr);
                // обновляем groupedSchedule чтобы Alpine подхватил изменения
                this.updateGroupedSchedule();
            } catch (e) {
                console.error('onDateClick error', e);
            }
        },

        // Инициализация компонента (вызовется Alpine x-init="init()")
        async init() {
            this.updateDateRanges();
            this.$nextTick(() => {
                this.swiperInstance = new Swiper(this.$refs.swiper, {
                    initialSlide: 1,
                    slidesPerView: 1,
                    speed: 400,
                    runCallbacksOnInit: false,  // Add this line to prevent events from firing during initialization
                    on: {
                        slideChangeTransitionEnd: () => {
                            const activeIndex = this.swiperInstance.activeIndex;
                            if (activeIndex > 1) {
                                this.weekStart = DateUtils.addDays(this.weekStart, 7);
                                this.updateDateRanges();
                                this.swiperInstance.slideTo(1, 0);
                            } else if (activeIndex < 1) {
                                this.weekStart = DateUtils.addDays(this.weekStart, -7);
                                this.updateDateRanges();
                                this.swiperInstance.slideTo(1, 0);
                            }
                        },
                    },
                });
            });

            this.groupedSchedule = {};
            this.isEmptySchedule = true;

            // Синхронизируем weekStart, dateRange
            this.weekStart = DateUtils.startOfWeek(new Date());
            this.updateDateRange();

            // initial load: загрузим текущую неделю
            try {
                // clear memory
                // mem = new ScheduleMemory() — но это const; аккуратно: просто оставим mem пустым
                // Загрузим week start
                await this.loadWeek(this.weekStart, 'initial');
                // После первой загрузки настроим observers
                // Однако refs могут быть ещё не привязаны — поэтому делаем небольшой таймаут на рендер
                await new Promise(r => requestAnimationFrame(r));
                this.setupObservers();
            } catch (e) {
                console.error('initial load failed', e);
            } finally {
                this.updateGroupedSchedule();
            }
        },

        // Служебные: reset, dispose
        async loadMoreTop() {
            await this.loadMore('top');
            this.updateGroupedSchedule();
        },
        async loadMoreBottom() {
            await this.loadMore('bottom');
            this.updateGroupedSchedule();
        },

        lessonKey(date, l) {
            return `${date}-${l.subject}-${l.startTime?.hour ?? '00'}-${l.startTime?.minute ?? '00'}-${l.subgroup ?? ''}`;
        },

        typeBadgeClass(typeRaw) {
            const t = (typeRaw || '').toLowerCase();
            if (t.includes('лекц')) return 'bg-blue-50 text-blue-700 ring-blue-200';
            //if (t.includes('практ')) return 'bg-green-50 text-green-700 ring-green-200';
            if (t.includes('лаб')) return 'bg-amber-50 text-amber-700 ring-amber-200';
            if (t.includes('сем')) return 'bg-purple-50 text-purple-700 ring-purple-200';
            //if (t.includes('экзам')) return 'bg-red-50 text-red-700 ring-red-200';
            //if (t.includes('зач')) return 'bg-emerald-50 text-emerald-700 ring-emerald-200';
            return 'bg-gray-50 text-gray-700 ring-gray-200';
        },

        formatCabinet(cab) {
            const v = (cab || '').trim();
            return v.length ? v : 'кабинет не указан';
        },

        formatSequence(pos, len, typeRaw) {
            return `${pos} из ${len}`;
        },

        // Сбрасываем наблюдателей при уничтожении
        destroy() {
            this.disconnectObservers();
        }
    };

    console.log("Возвращён объект расписания scheduleComponent");
    console.log(apiSurface);

    return apiSurface;
}

export default scheduleComponent;