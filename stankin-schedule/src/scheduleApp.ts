import { ApiClient } from './ApiClient';

const GROUPS_STORAGE_KEY = 'stankin-groups-v1';
const TEACHERS_STORAGE_KEY = 'stankin-teachers-v1';

function storeList(key: string, items: string[]) {
  try { localStorage.setItem(key, JSON.stringify(items)); } catch {}
}

function readStoredList(key: string): string[] | null {
  try {
    const raw = localStorage.getItem(key);
    if (!raw) return null;
    const parsed = JSON.parse(raw);
    return Array.isArray(parsed) ? parsed : null;
  } catch {
    return null;
  }
}

export function scheduleApp(api: ApiClient) {
  return {
    groups: [] as string[],
    teachers: [] as string[],
    selectedGroup: null as string | null,
    selectedTeacher: null as string | null,
    viewMode: 'group' as 'group' | 'teacher',
    error: null as string | null,
    loadingGroups: false,
    loadingTeachers: false,
    teacherSearch: '',
    showPicker: false,
    scheduleUnavailable: null as boolean | null,
    offline: false,
    boardNewThreads: 0,
    netBound: false,

    get filteredTeachers(): string[] {
      const q = this.teacherSearch.trim().toLowerCase();
      if (!q) return this.teachers;
      return this.teachers.filter(t => t.toLowerCase().includes(q));
    },

    get displayTitle(): string {
      if (this.viewMode === 'teacher' && this.selectedTeacher) return this.selectedTeacher;
      if (this.viewMode === 'group' && this.selectedGroup) return this.selectedGroup;
      return '';
    },

    get hasSelection(): boolean {
      return this.viewMode === 'group'
        ? !!this.selectedGroup
        : !!this.selectedTeacher;
    },

    async init() {
      // сохранённые выбор и списки применяем сразу — первый кадр не ждёт сеть
      const savedMode = localStorage.getItem('viewMode') as 'group' | 'teacher' | null;
      if (savedMode) this.viewMode = savedMode;
      const savedGroup = localStorage.getItem('selectedGroup');
      if (savedGroup) this.selectedGroup = savedGroup;
      const savedTeacher = localStorage.getItem('selectedTeacher');
      if (savedTeacher) this.selectedTeacher = savedTeacher;

      this.groups = readStoredList(GROUPS_STORAGE_KEY) ?? [];
      if (this.viewMode === 'teacher') this.teachers = readStoredList(TEACHERS_STORAGE_KEY) ?? [];

      if (navigator.onLine === false) {
        // офлайн: сети нет — сразу офлайн-режим без сетевых попыток
        this.offline = true;
        this.scheduleUnavailable = false;
        this.attachNetworkListeners();
        return;
      }

      // онлайн: рисуем на локальных данных мгновенно, сеть обновляет в фоне
      this.attachNetworkListeners();
      void this.refreshOnline();
    },

    // сетевой апдейт групп/бейджа/преподавателей в фоне (не блокирует первый кадр)
    async refreshOnline() {
      await this.loadGroups();
      if (this.scheduleUnavailable) {
        this.startAvailabilityPoll();
        return;
      }
      if (this.viewMode === 'teacher' && this.teachers.length === 0) {
        await this.loadTeachers();
      }
    },

    attachNetworkListeners() {
      if (this.netBound) return;
      this.netBound = true;
      window.addEventListener('offline', () => {
        this.offline = true;
        this.scheduleUnavailable = false;
        if (this.groups.length === 0) {
          const saved = readStoredList(GROUPS_STORAGE_KEY);
          if (saved && saved.length > 0) this.groups = saved;
        }
      });
      window.addEventListener('online', () => {
        this.offline = false;
        void this.refreshOnline();
      });
    },

    isNetworkFailure(e: any): boolean {
      if (e?.network === true) return true;
      if (navigator.onLine === false) return true;
      const msg = String(e?.message ?? '').toLowerCase();
      return /offline|failed to fetch|network|internet|load failed/i.test(msg);
    },

    async loadGroups() {
      this.loadingGroups = true;
      this.error = null;
      try {
        const groups = await api.getGroups();
        this.groups = Array.isArray(groups) ? groups : [];
        storeList(GROUPS_STORAGE_KEY, this.groups);
        this.scheduleUnavailable = false;
        this.offline = false;
      } catch (e: any) {
        if (e?.status === 503) {
          this.scheduleUnavailable = true;
          return;
        }
        this.offline = this.isNetworkFailure(e);
        this.scheduleUnavailable = false;
        // офлайн-резерв списка групп из localStorage (не зависит от SW)
        if (this.groups.length === 0) {
          const saved = readStoredList(GROUPS_STORAGE_KEY);
          if (saved && saved.length > 0) this.groups = saved;
        }
        console.error('loadGroups error', e);
        this.error = this.offline
          ? 'Нет соединения — доступно только ранее загруженное расписание'
          : 'Не удалось загрузить список групп';
      } finally {
        this.loadingGroups = false;
      }
    },

    startAvailabilityPoll() {
      setInterval(async () => {
        if (this.scheduleUnavailable !== true) return;
        await this.loadGroups();
      }, 30 * 60 * 1000);
    },

    async loadTeachers() {
      if (this.teachers.length > 0) return;
      this.loadingTeachers = true;
      this.error = null;
      try {
        const teachers = await api.getTeachers();
        this.teachers = Array.isArray(teachers) ? teachers : [];
        storeList(TEACHERS_STORAGE_KEY, this.teachers);
        this.offline = false;
      } catch (e) {
        this.offline = this.isNetworkFailure(e);
        this.scheduleUnavailable = false;
        if (this.teachers.length === 0) {
          const saved = readStoredList(TEACHERS_STORAGE_KEY);
          if (saved && saved.length > 0) this.teachers = saved;
        }
        console.error('loadTeachers error', e);
        this.error = this.offline
          ? 'Нет соединения — доступно только ранее загруженное расписание'
          : 'Не удалось загрузить список преподавателей';
      } finally {
        this.loadingTeachers = false;
      }
    },

    selectGroup(group: string) {
      this.selectedGroup = null;
      setTimeout(() => {
        this.selectedGroup = group;
        this.showPicker = false;
        try { localStorage.setItem('selectedGroup', group); } catch {}
      }, 10);
    },

    selectTeacher(teacher: string) {
      this.selectedTeacher = null;
      setTimeout(() => {
        this.selectedTeacher = teacher;
        this.showPicker = false;
        this.teacherSearch = '';
        try { localStorage.setItem('selectedTeacher', teacher); } catch {}
      }, 10);
    },

    async openPicker() {
      this.showPicker = true;
      if (this.viewMode === 'teacher' && this.teachers.length === 0) {
        await this.loadTeachers();
      }
    },

    closePicker() {
      this.showPicker = false;
      this.teacherSearch = '';
    },

    async switchMode(mode: 'group' | 'teacher') {
      if (this.viewMode === mode) return;

      const savedGroup = this.selectedGroup;
      const savedTeacher = this.selectedTeacher;

      this.selectedGroup = null;
      this.selectedTeacher = null;

      setTimeout(async () => {
        this.viewMode = mode;
        this.selectedGroup = savedGroup;
        this.selectedTeacher = savedTeacher;

        try { localStorage.setItem('viewMode', mode); } catch {}
        if (mode === 'teacher' && this.teachers.length === 0) {
          await this.loadTeachers();
        }
      }, 10);
    },

    reset() {
      this.selectedGroup = null;
      this.selectedTeacher = null;
      this.showPicker = false;
      this.teacherSearch = '';
      try {
        localStorage.removeItem('selectedGroup');
        localStorage.removeItem('selectedTeacher');
      } catch {}
    }
  };
}
