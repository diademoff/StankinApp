import { ApiClient } from './ApiClient';

export function adminApp(api: ApiClient) {
  return {
    secret: (sessionStorage.getItem('adminSecret') || '') as string,
    reports: [] as any[],
    loading: false,
    error: '' as string,

    get isAuthed(): boolean {
      return !!this.secret;
    },

    async login() {
      this.error = '';
      if (!this.secret) {
        this.error = 'Введите пароль';
        return;
      }
      try {
        sessionStorage.setItem('adminSecret', this.secret);
        await this.load();
      } catch (e) {
        this.error = (e as Error).message;
      }
    },

    async load() {
      this.loading = true;
      this.error = '';
      try {
        this.reports = await api.adminGetReports(this.secret);
      } catch (e) {
        this.error = (e as Error).message;
        this.reports = [];
      } finally {
        this.loading = false;
      }
    },

    async deletePost(id: number) {
      if (!confirm('Удалить пост (и дочерние при удалении треда)?')) return;
      try {
        await api.adminDeletePost(id, this.secret);
        await this.load();
      } catch (e) {
        this.error = (e as Error).message;
      }
    },

    async dismiss(id: number) {
      try {
        await api.adminDismissReports(id, this.secret);
        await this.load();
      } catch (e) {
        this.error = (e as Error).message;
      }
    },

    async ban(ipHash: string) {
      if (!confirm(`Забанить IP (хэш ${ipHash.slice(0, 8)}…)?`)) return;
      try {
        await api.adminBan(ipHash, this.secret);
        await this.load();
      } catch (e) {
        this.error = (e as Error).message;
      }
    },

    logout() {
      sessionStorage.removeItem('adminSecret');
      this.secret = '';
      this.reports = [];
    },
  };
}
