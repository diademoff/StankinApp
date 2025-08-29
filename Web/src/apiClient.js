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
        const url = `/api/groups`;
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
        const url = `/api/schedule?${params.toString()}`;
        console.log("Отправка запроса на получение расписания " + params);
        return this.fetchJson(url);
    }
}


export default ApiClient;