import { authService } from '../../shared/AuthService';

export class ApiClient {
  constructor(private base: string) { }

  private getHeaders(): Record<string, string> {
    const headers: Record<string, string> = {
      'Content-Type': 'application/json'
    };
    const token = authService.getToken();
    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }
    return headers;
  }

  private async fetchJson(url: string, options: RequestInit = {}): Promise<any> {
    try {
      const res = await fetch(url, {
        ...options,
        headers: this.getHeaders(),
      });
      if (res.status === 401) {
        console.error('Authorization error (401). Logging out.');
        authService.logout();
        throw new Error('Сессия истекла. Пожалуйста, войдите снова.');
      }
      if (!res.ok) {
        const errorData = await res.json().catch(() => ({ error: 'API error with no JSON body' }));
        console.error(`API error ${res.status}:`, errorData);
        throw new Error(errorData.error || `API error ${res.status}`);
      }
      if (res.status === 204) {
        return null;
      }
      return await res.json();
    } catch (error) {
      console.error('API request failed:', url, error);
      throw error;
    }
  }

  async postYandexToken(access_token: string): Promise<{ token: string, user: any }> {
    const url = `${this.base}/api/auth/yandex/token`;
    return this.fetchJson(url, {
      method: 'POST',
      body: JSON.stringify({ access_token }),
    });
  }

  async getGroups(): Promise<string[]> {
    const url = `${this.base}/api/groups`;
    return this.fetchJson(url);
  }

  async getSchedule(group: string, startDate: string, endDate: string): Promise<any[]> {
    const params = new URLSearchParams({ groupName: group, startDate, endDate });
    const url = `${this.base}/api/schedule?${params.toString()}`;
    return this.fetchJson(url);
  }

  // --- Методы для рейтингов ---
  async getTeacherRating(teacherName: string): Promise<any> {
    const url = `${this.base}/api/teachers/get-teacher-rating?name=${encodeURIComponent(teacherName)}`;
    return this.fetchJson(url);
  }

  async getUserRating(teacherName: string): Promise<any> {
    /* Возвращает оценку, которую дал студент преподавателю:
      {
        "data": {
          "score": 10
        }
      }
     */
    const url = `${this.base}/api/teachers/get-user-rating?name=${encodeURIComponent(teacherName)}`;
    return this.fetchJson(url);
  }

  async postTeacherRating(teacherName: string, score: number): Promise<any> {
    const url = `${this.base}/api/teachers/vote-rating`;
    const body = JSON.stringify({ teacherName, score });
    return this.fetchJson(url, {
      method: 'POST',
      body: body,
    });
  }
}