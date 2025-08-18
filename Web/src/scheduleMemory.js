import DateUtils from './dateUtils.js';

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


export default ScheduleMemory;