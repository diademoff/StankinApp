export class ApiClient {
  constructor(private base: string) {}

  async fetchJson(url: string): Promise<any> {
    const res = await fetch(url);
    if (!res.ok) throw new Error(`API error ${res.status}`);
    return res.json();
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
}
