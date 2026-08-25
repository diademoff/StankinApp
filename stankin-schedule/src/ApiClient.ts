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
        const err: any = new Error(errorData.error || `API error ${res.status}`);
        err.status = res.status;
        throw err;
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

  async getScheduleBySubject(subject: string, teacher: string, groupName: string): Promise<any[]> {
    const params = new URLSearchParams({ subject, teacher, groupName });
    const url = `${this.base}/api/schedule/by-subject?${params.toString()}`;
    const response = await this.fetchJson(url);
    return response?.items ?? [];
  }

  // ==== Доска ====

  async getThreads(page: number): Promise<any[]> {
    const response = await this.fetchJson(`${this.base}/api/board/threads?page=${page}`);
    return response?.items ?? [];
  }

  async getThread(threadId: number): Promise<any> {
    return await this.fetchJson(`${this.base}/api/board/threads/${threadId}`);
  }

  async createThread(text: string, captchaToken: string): Promise<any> {
    return await this.fetchJson(`${this.base}/api/board/threads`, {
      method: 'POST',
      body: JSON.stringify({ text, captchaToken }),
    });
  }

  async createReply(
    threadId: number, text: string, captchaToken: string, parentId: number | null, sage: boolean
  ): Promise<any> {
    return await this.fetchJson(`${this.base}/api/board/threads/${threadId}/posts`, {
      method: 'POST',
      body: JSON.stringify({ text, captchaToken, parentId, sage }),
    });
  }

  async reportPost(postId: number): Promise<void> {
    await this.fetchJson(`${this.base}/api/board/posts/${postId}/report`, { method: 'POST' });
  }

  // ==== Модерация ====

  private async adminRequest(url: string, secret: string, method: string = 'GET', body?: any): Promise<any> {
    const headers = { 'X-Admin-Secret': secret } as any;
    return await this.fetchJson(url, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
    });
  }

  async adminGetReports(secret: string): Promise<any[]> {
    const response = await this.adminRequest(`${this.base}/api/admin/reports`, secret);
    return response?.items ?? [];
  }

  async adminDeletePost(postId: number, secret: string): Promise<void> {
    await this.adminRequest(`${this.base}/api/admin/posts/${postId}`, secret, 'DELETE');
  }

  async adminDismissReports(postId: number, secret: string): Promise<void> {
    await this.adminRequest(`${this.base}/api/admin/reports/${postId}/dismiss`, secret, 'POST');
  }

  async adminBan(ipHash: string, secret: string): Promise<void> {
    await this.adminRequest(`${this.base}/api/admin/ban`, secret, 'POST', { ipHash });
  }
}