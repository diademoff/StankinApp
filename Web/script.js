// script.js
// Автор: ChatGPT
// Описание: Модульный скрипт для страницы расписания.
// Требования: Alpine.js подключён и доступен (x-data="...").
// Использует window.API_BASE_URL как базу для fetch.
// Кэширование: localStorage, TTL = 2 часа.

(() => {
    'use strict';

    /* ==========================
       Конфигурация
       ========================== */
    const CACHE_PREFIX = 'scheduleCache:v1';
    const CACHE_TTL_MS = 2 * 60 * 60 * 1000; // 2 часаformatDateShort
    const GROUPS_CACHE_KEY = `${CACHE_PREFIX}:groups`;
    const DEFAULT_WEEK_DAY_START = 1; // 0 - воскресенье, 1 - понедельник (Россия — понедельник)

    /* ==========================
       Низкоуровневые утилиты: DateUtils
       ========================== */
    class DateUtils {
        static clone(d) { return new Date(d.getTime()); }

        // Возвращает YYYY-MM-DD
        static formatDateForApi(d) {
            return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
        }

        // ISO-like date YYYY-MM-DD (для id)
        static toIsoDate(d) {
            return DateUtils.formatDateForApi(d);
        }

        // Возвращает начало недели (понедельник по умолчанию) в 00:00:00
        static startOfWeek(date, weekStart = DEFAULT_WEEK_DAY_START) {
            const d = new Date(date.getFullYear(), date.getMonth(), date.getDate());
            const day = d.getDay(); // 0..6 (вс..сб)
            // смещение от weekStart к текущему дню
            let diff = (day - weekStart + 7) % 7;
            d.setDate(d.getDate() - diff);
            d.setHours(0, 0, 0, 0);
            return d;
        }

        static addDays(date, n) {
            const d = new Date(date);
            d.setDate(d.getDate() + n);
            return d;
        }

        static daysBetween(a, b) {
            const ax = new Date(a.getFullYear(), a.getMonth(), a.getDate());
            const bx = new Date(b.getFullYear(), b.getMonth(), b.getDate());
            const diff = Math.round((bx - ax) / (24 * 3600 * 1000));
            return diff;
        }

        // Возвращает массив дат (Date) от start включительно length дней
        static rangeDays(start, length = 7) {
            const out = [];
            const d = new Date(start);
            for (let i = 0; i < length; i++) {
                out.push(new Date(d.getFullYear(), d.getMonth(), d.getDate()));
                d.setDate(d.getDate() + 1);
            }
            return out;
        }

        static parseApiDateObj(obj) {
            // { year: 2025, month: 3, day: 1 }
            return new Date(obj.year, obj.month - 1, obj.day, 0, 0, 0, 0);
        }

        // Форматы для UI
        static formatDateHuman(d) {
            return new Date(d).toLocaleDateString('ru-RU', { weekday: 'long', day: 'numeric', month: 'long' });
        }

        static formatDateShort(d) {
            const x = new Date(d);
            const m = ['янв.', 'фев.', 'мар.', 'апр.', 'май', 'июн.', 'июл.', 'авг.', 'сен.', 'окт.', 'ноя.', 'дек.'];
            return `${x.getDate()} ${m[x.getMonth()]}`;
        }

        static formatTime(t) {
            if (!t) return '';
            const hh = String(t.hour).padStart(2, '0');
            const mm = String(t.minute).padStart(2, '0');
            return `${hh}:${mm}`;
        }

        static calculateEndTime(startTime, duration) {
            // startTime: {hour, minute}; duration: {minutes}
            const start = new Date();
            start.setHours(startTime.hour || 0, startTime.minute || 0, 0, 0);
            const endMs = start.getTime() + (duration?.minutes || 0) * 60000;
            const end = new Date(endMs);
            return { hour: end.getHours(), minute: end.getMinutes() };
        }
    }

    /* ==========================
       LocalStorage Cache abstraction
       ========================== */
    class LocalCache {
        constructor(prefix = CACHE_PREFIX) {
            this.prefix = prefix;
        }

        buildKey(...parts) {
            return [this.prefix, ...parts].join(':');
        }

        set(key, value) {
            const payload = {
                ts: Date.now(),
                value
            };
            try {
                localStorage.setItem(key, JSON.stringify(payload));
            } catch (e) {
                // localStorage может быть переполнен — игнорируем
                console.warn('Cache set failed', e);
            }
        }

        get(key, ttl = CACHE_TTL_MS) {
            try {
                const raw = localStorage.getItem(key);
                if (!raw) return null;
                const parsed = JSON.parse(raw);
                if (!parsed || typeof parsed.ts !== 'number') return null;
                if ((Date.now() - parsed.ts) > ttl) {
                    // устарело
                    localStorage.removeItem(key);
                    return null;
                }
                return parsed.value;
            } catch (e) {
                console.warn('Cache get failed', e);
                return null;
            }
        }

        remove(key) {
            try {
                localStorage.removeItem(key);
            } catch (e) { }
        }
    }

    /* ==========================
       API client
       ========================== */
    class ApiClient {
        constructor(base) {
            this.base = base ? base.replace(/\/$/, '') : '';
        }

        async fetchJson(url, opts = {}) {
            let res = await fetch(url, opts);
            if (!res.ok) {
                const text = await res.text().catch(() => '');
                throw new Error(`API error ${res.status}: ${text}`);
            }
            console.log("Запрос " + url + " выполнен");

            return res.json();
        }

        async getGroups() {
            const url = `${this.base}/api/groups`;
            console.log("Отправка запроса на получение списка групп");
            return this.fetchJson(url);
        }

        // Возвращает массив занятий (как отдаёт API) в диапазоне startDate..endDate (включительно)
        async getSchedule(groupName, startDateApi, endDateApi) {
            const params = new URLSearchParams({
                groupName,
                startDate: startDateApi,
                endDate: endDateApi
            });
            const url = `${this.base}/api/schedule?${params.toString()}`;
            console.log("Отправка запроса на получение расписания " + params);
            return this.fetchJson(url);
        }
    }

    /* ==========================
       ScheduleRepository: слой, работающий с API + кэшем + трансформацией
       ========================== */
    class ScheduleRepository {
        constructor(apiClient, cache) {
            this.api = apiClient;
            this.cache = cache;
        }

        // Ключ кэша для недели
        cacheKeyForWeek(groupName, startDateApi, endDateApi) {
            return this.cache.buildKey('schedule', groupName, `${startDateApi}_${endDateApi}`);
        }

        // Сначала кэш, иначе API. Возвращает объект: { items: Array, fromCache: bool }
        async fetchWeek(groupName, startDateApi, endDateApi) {
            console.log("fetching week " + groupName +
                ", date=" + startDateApi + ", " + endDateApi);

            const key = this.cacheKeyForWeek(groupName, startDateApi, endDateApi);
            const cached = this.cache.get(key);
            if (cached) {
                console.log("found in cache for date=" + startDateApi + ", " + endDateApi);
                return { items: cached, fromCache: true };
            }
            const items = await this.api.getSchedule(groupName, startDateApi, endDateApi);
            // items может быть []
            this.cache.set(key, items);
            return { items, fromCache: false };
        }

        // Загружает группы (с небольшим кэшем)
        async fetchGroups() {
            const key = this.cache.buildKey('groups');
            const cached = this.cache.get(key, 60 * 60 * 1000); // 1 час для списка групп
            if (cached) {
                console.log("Группы найдены в кэше");
                return cached;
            }
            const groups = await this.api.getGroups();
            this.cache.set(key, groups);
            return groups;
        }

        // Трансформация: API возвращает элементы, у которых может быть поле dates: [{year,month,day}, ...]
        // Мы хотим получить карту dateStr->array(lesson)
        // Но этот метод не мутирует store — возвращает массив "normalisedLessons"
        normalizeItemsToLessons(items) {
            const out = [];
            for (const it of items || []) {
                const base = {
                    groupName: it.groupName,
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
                if (!Array.isArray(it.dates) || it.dates.length === 0) {
                    // если дата не указана — пропускаем
                    continue;
                }
                for (const d of it.dates) {
                    const dateObj = DateUtils.parseApiDateObj(d);
                    const dateStr = DateUtils.toIsoDate(dateObj);
                    out.push(Object.assign({ date: dateStr }, base));
                }
            }
            return out;
        }
    }

    /* ==========================
       Помощники для UI / работы со списком расписания (в оперативной памяти)
       ========================== */
    class ScheduleMemory {
        constructor() {
            // map dateStr -> array lessons
            this.map = Object.create(null);
            // порядок дат в виде массива YYYY-MM-DD
            this.order = [];
        }

        // Устанавливает (перезаписывает) массив lesson'ов для dateStr
        setDay(dateStr, lessons) {
            // сортировка по времени (hour, minute) и по sequencePosition
            const sorted = (lessons || []).slice().sort((a, b) => {
                const ah = (a.startTime?.hour ?? 0), am = (a.startTime?.minute ?? 0);
                const bh = (b.startTime?.hour ?? 0), bm = (b.startTime?.minute ?? 0);
                const ta = ah * 60 + am;
                const tb = bh * 60 + bm;
                if (ta !== tb) return ta - tb;
                return (a.sequencePosition || 0) - (b.sequencePosition || 0);
            });
            this.map[dateStr] = sorted;
            if (!this.order.includes(dateStr)) {
                // вставляем в правильное место (чтобы order была отсортирована по дате возрастанию)
                this.order.push(dateStr);
                this.order.sort((a, b) => a.localeCompare(b)); // YYYY-MM-DD корректно сортируется лексикографически
            }
        }

        // Добавляет (сливает) уроки для даты, избегая дублирования
        mergeDay(dateStr, lessons) {
            const existing = (this.map[dateStr] || []).slice();
            const keyset = new Set(existing.map(l => ScheduleMemory._lessonKey(l)));
            for (const l of lessons || []) {
                const k = ScheduleMemory._lessonKey(l);
                if (!keyset.has(k)) {
                    existing.push(l);
                    keyset.add(k);
                }
            }
            this.setDay(dateStr, existing);
        }

        static _lessonKey(l) {
            // уникальность по основным полям
            const s = [
                l.subject ?? '',
                l.teacher ?? '',
                l.type ?? '',
                l.cabinet ?? '',
                (l.startTime?.hour ?? '') + ':' + (l.startTime?.minute ?? ''),
                (l.duration?.minutes ?? '')
            ].join('|');
            return s;
        }

        hasDay(dateStr) {
            return Object.prototype.hasOwnProperty.call(this.map, dateStr);
        }

        getDay(dateStr) {
            return this.map[dateStr] || [];
        }

        // Обеспечить наличие пустых дней для диапазона
        ensureDaysRange(startDate, endDate) {
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

        // Возвращает объект для рендера: { dateStr: lessons, ... } в порядке this.order
        asGroupedObject() {
            const out = Object.create(null);
            // sort order ascending
            this.order.sort((a, b) => a.localeCompare(b));
            for (const d of this.order) {
                out[d] = this.map[d] || [];
            }
            return out;
        }

        earliestDate() {
            if (this.order.length === 0) return null;
            return this.order[0];
        }

        latestDate() {
            if (this.order.length === 0) return null;
            return this.order[this.order.length - 1];
        }
    }

    /* ==========================
       Alpine root component: scheduleApp()
       - получает список групп
       - хранит выбранную группу
       ========================== */
    function scheduleApp() {
        const api = new ApiClient(window.API_BASE_URL || '');
        const cache = new LocalCache(CACHE_PREFIX);
        const repo = new ScheduleRepository(api, cache);

        return {
            groups: [],
            selectedGroup: null,
            loadingGroups: false,
            error: null,

            init() {
                this.loadGroups();
                // восстановим выбранную группу, если есть
                const saved = localStorage.getItem('selectedGroup');
                if (saved) {
                    this.selectedGroup = saved;
                    console.log("Выбрана группа из кэша" + saved);
                }
            },

            async loadGroups() {
                console.log('Загрузка groups');
                this.loadingGroups = true;
                this.error = null;
                try {
                    const groups = await repo.fetchGroups();
                    this.groups = Array.isArray(groups) ? groups : [];
                    if (Array.isArray(groups))
                        console.log("Список groups получен");
                } catch (e) {
                    console.error('loadGroups error', e);
                    this.error = 'Не удалось загрузить список групп.';
                } finally {
                    this.loadingGroups = false;
                }
            },

            selectGroup(group) {
                this.selectedGroup = group;
                console.log("Выбрана группа " + this.selectedGroup);
                try {
                    localStorage.setItem('selectedGroup', group);
                    console.log("Выбранная группа сохранена в кэш");
                } catch (e) { }
                // Alpine автоматически инициализирует вложенный компонент scheduleComponent(selectedGroup)
            },

            reset() {
                this.selectedGroup = null;
                try { localStorage.removeItem('selectedGroup'); } catch (e) { }
            }
        };
    }

    /* ==========================
       Alpine component для расписания конкретной группы
       x-data="scheduleComponent(selectedGroup)"
       ========================== */
    function scheduleComponent(groupNameInitial) {
        // Создаём объекты низкого уровня
        const api = new ApiClient(window.API_BASE_URL || '');
        const cache = new LocalCache(CACHE_PREFIX);
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
    // expose to global (для Alpine x-data)
    window.scheduleApp = scheduleApp;
    window.scheduleComponent = scheduleComponent;

})();
