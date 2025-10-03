export class ApiClient {
  constructor(private base: string) {}

  async fetchJson(url: string): Promise<any> {
    try {
      const res = await fetch(url);
      if (!res.ok) throw new Error(`API error ${res.status}`);
      console.log('Request completed:', url);
      return res.json();
    } catch (error) {
      console.error('API request failed:', url, error);
      throw error;
    }
  }

  async getGroups(): Promise<string[]> {
    const url = `${this.base}/api/groups`;
    console.log('Fetching groups from:', url);
    return this.fetchJson(url);
  }

  async getSchedule(group: string, startDate: string, endDate: string): Promise<any[]> {
    const params = new URLSearchParams({ groupName: group, startDate, endDate });
    const url = `${this.base}/api/schedule?${params.toString()}`;
    console.log('Fetching schedule from:', url);
    return this.fetchJson(url);
  }
}