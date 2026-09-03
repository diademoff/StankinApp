import { ApiClient } from './ApiClient';

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
      await this.loadGroups();
      await this.loadBoardBadge();
      if (this.scheduleUnavailable) {
        this.startAvailabilityPoll();
        return;
      }

      const savedMode = localStorage.getItem('viewMode') as 'group' | 'teacher' | null;
      if (savedMode) this.viewMode = savedMode;

      const savedGroup = localStorage.getItem('selectedGroup');
      if (savedGroup) this.selectedGroup = savedGroup;

      const savedTeacher = localStorage.getItem('selectedTeacher');
      if (savedTeacher) this.selectedTeacher = savedTeacher;

      if (this.viewMode === 'teacher' && this.teachers.length === 0) {
        await this.loadTeachers();
      }
    },

    isNetworkFailure(e: any): boolean {
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
        this.scheduleUnavailable = false;
        this.offline = false;
      } catch (e: any) {
        if (e?.status === 503) {
          this.scheduleUnavailable = true;
          return;
        }
        this.offline = this.isNetworkFailure(e);
        this.scheduleUnavailable = false;
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

    async loadBoardBadge() {
      try {
        const lastVisit = localStorage.getItem('boardLastVisitAt');
        this.boardNewThreads = await api.getNewThreadsCount(lastVisit);
      } catch (e) {
        console.error('loadBoardBadge error', e);
        this.boardNewThreads = 0;
      }
    },

    async loadTeachers() {
      if (this.teachers.length > 0) return;
      this.loadingTeachers = true;
      this.error = null;
      try {
        const teachers = await api.getTeachers();
        this.teachers = Array.isArray(teachers) ? teachers : [];
        this.offline = false;
      } catch (e) {
        this.offline = this.isNetworkFailure(e);
        this.scheduleUnavailable = false;
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
