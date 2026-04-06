export class ApiClient {
  constructor(private base: string) {}

  private async fetchJson(url: string, options: RequestInit = {}): Promise<any> {
    try {
      const res = await fetch(url, {
        ...options,
        headers: { 'Content-Type': 'application/json' },
      });

      if (res.status === 204) {
        return null;
      }

      if (!res.ok) {
        const errorData = await res.json().catch(() => ({ error: `API error ${res.status}` }));
        throw new Error(errorData.error || `API error ${res.status}`);
      }

      return await res.json();
    } catch (error) {
      console.error('API request failed:', url, error);
      throw error;
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
}