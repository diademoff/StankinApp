import SCHEDULE_CONFIG from './config.js';


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
    static startOfWeek(date, weekStart = SCHEDULE_CONFIG.DEFAULT_WEEK_DAY_START) {
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
        const dateStr = new Date(d).toLocaleDateString('ru-RU', {
            weekday: 'long',
            day: 'numeric',
            month: 'long'
        });
        return dateStr.charAt(0).toUpperCase() + dateStr.slice(1);
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

export default DateUtils;