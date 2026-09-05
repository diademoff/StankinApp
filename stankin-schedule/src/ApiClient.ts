export class ApiClient {
  constructor(private base: string, private timeoutMs = 8000) {}

  private async fetchJson(url: string, options: RequestInit = {}): Promise<any> {
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), this.timeoutMs);
    try {
      const res = await fetch(url, {
        ...options,
        signal: controller.signal,
        headers: {
          'Content-Type': 'application/json',
          ...(options.headers as Record<string, string> | undefined),
        },
      });

      if (res.status === 204) {
        return null;
      }

      if (!res.ok) {
        const errorData = await res.json().catch(() => ({ error: `API error ${res.status}` }));
        const err: any = new Error(errorData.error || `API error ${res.status}`);
        err.status = res.status;
        throw err;
      }

      return await res.json();
    } catch (error) {
      // ошибка уровня сети/SW/таймаут (нет статуса HTTP): TypeError, 'Failed to fetch',
      // AbortError, офлайн-отказ SW — помечаем, чтобы UI не полагался на navigator.onLine
      const err: any = error;
      if (err && err.status == null) {
        const msg = String(err?.message ?? '');
        err.network =
          err.name === 'AbortError' ||
          err instanceof TypeError ||
          /offline|failed to fetch|network|load failed|aborted/i.test(msg);
      }
      console.error('API request failed:', url, error);
      throw error;
    } finally {
      clearTimeout(timer);
    }
  }

  async getGroups(): Promise<string[]> {
    const url = `${this.base}/api/groups`;
    const response = await this.fetchJson(url);
    return response?.items ?? [];
  }

  async getTeachers(): Promise<string[]> {
    const url = `${this.base}/api/teachers`;
    const response = await this.fetchJson(url);
    return response?.items ?? [];
  }

  async getSchedule(group: string, startDate: string, endDate: string): Promise<any[]> {
    const params = new URLSearchParams({ groupName: group, startDate, endDate });
    const url = `${this.base}/api/schedule?${params.toString()}`;
    const response = await this.fetchJson(url);
    // 204 → null → пустой массив
    return response?.items ?? [];
  }

  async getTeacherSchedule(teacherName: string, startDate: string, endDate: string): Promise<any[]> {
    const params = new URLSearchParams({ teacherName, startDate, endDate });
    const url = `${this.base}/api/schedule/teacher?${params.toString()}`;
    const response = await this.fetchJson(url);
    return response?.items ?? [];
  }

  async getScheduleBySubject(subject: string, teacher: string, groupName: string): Promise<any[]> {
    const params = new URLSearchParams({ subject, teacher, groupName });
    const url = `${this.base}/api/schedule/by-subject?${params.toString()}`;
    const response = await this.fetchJson(url);
    return response?.items ?? [];
  }
}