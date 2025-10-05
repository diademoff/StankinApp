export class ApiClient {
  constructor(private base: string) { }

  private getAuthHeaders(): Record<string, string> {
    const token = localStorage.getItem('jwt_token');
    if (token) {
      return { 'Authorization': `Bearer ${token}` };
    }
    return {};
  }

  private async fetchJson(url: string, options: RequestInit = {}): Promise<any> {
    try {
      const res = await fetch(url, {
        ...options,
        headers: {
          ...options.headers,
          'Content-Type': 'application/json',
        },
      });
      if (!res.ok) {
        const errorData = await res.json().catch(() => ({ error: 'API error with no JSON body' }));
        console.error(`API error ${res.status}:`, errorData);
        throw new Error(errorData.error || `API error ${res.status}`);
      }
      // Некоторые успешные ответы могут не иметь тела (например, 204 No Content)
      if (res.status === 204) {
        return null;
      }
      const responseData = await res.json();
      return responseData;
    } catch (error) {
      console.error('API request failed:', url, error);
      throw error;
    }
  }

  async postYandexToken(access_token: string): Promise<{ jwt: string }> {
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

  // --- Методы для рейтингов и комментариев ---
  async getTeachers(): Promise<string[]> {
    const url = `${this.base}/api/teachers`;
    return this.fetchJson(url);
  }

  async getTeacherRatings(teacherId: number): Promise<any> {
    const url = `${this.base}/api/teachers/${teacherId}/ratings`;
    return this.fetchJson(url);
  }

  async getTeacherComments(teacherId: number, page = 1, limit = 20): Promise<any> {
    const params = new URLSearchParams({ page: String(page), limit: String(limit) });
    const url = `${this.base}/api/teachers/${teacherId}/comments?${params.toString()}`;
    return this.fetchJson(url);
  }

  async postRating(teacherId: number, score: number): Promise<any> {
    const url = `${this.base}/api/teachers/${teacherId}/ratings`;
    return this.fetchJson(url, {
      method: 'POST',
      headers: this.getAuthHeaders(),
      body: JSON.stringify({ score }),
    });
  }

  async postComment(teacherId: number, content: string, anonymous: boolean): Promise<any> {
    const url = `${this.base}/api/teachers/${teacherId}/comments`;
    return this.fetchJson(url, {
      method: 'POST',
      headers: this.getAuthHeaders(),
      body: JSON.stringify({ content, anonymous }),
    });
  }

  async postVote(commentId: number, vote: 1 | -1): Promise<any> {
    const url = `${this.base}/api/comments/${commentId}/vote`;
    return this.fetchJson(url, {
      method: 'POST',
      headers: this.getAuthHeaders(),
      body: JSON.stringify({ vote }),
    });
  }

  async validateTeacher(teacherName: string): Promise<boolean> {
    const url = `${this.base}/api/teachers/validate?name=${encodeURIComponent(teacherName)}`;
    const response = await this.fetchJson(url);
    return response.exists === true;
  }

  async getTeacherRatingsByName(teacherName: string): Promise<any> {
    const url = `${this.base}/api/teachers/by-name/ratings?name=${encodeURIComponent(teacherName)}`;
    return this.fetchJson(url);
  }

  async getTeacherCommentsByName(teacherName: string, page = 1, limit = 20): Promise<any> {
    const params = new URLSearchParams({ page: String(page), limit: String(limit) });
    const url = `${this.base}/api/teachers/by-name/comments?name=${encodeURIComponent(teacherName)}&${params.toString()}`;
    return this.fetchJson(url);
  }

  async postRatingByName(teacherName: string, score: number): Promise<any> {
    const url = `${this.base}/api/teachers/by-name/ratings`;
    return this.fetchJson(url, {
      method: 'POST',
      headers: this.getAuthHeaders(),
      body: JSON.stringify({ teacherName, score }),
    });
  }

  async postCommentByName(teacherName: string, content: string, anonymous: boolean): Promise<any> {
    const url = `${this.base}/api/teachers/by-name/comments`;
    return this.fetchJson(url, {
      method: 'POST',
      headers: this.getAuthHeaders(),
      body: JSON.stringify({ teacherName, content, anonymous }),
    });
  }
}