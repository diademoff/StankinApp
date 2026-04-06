import { LoadGroupsUseCase } from '../../core/use-cases/LoadGroupsUseCase';
import { LoadScheduleWeekUseCase } from '../../core/use-cases/LoadScheduleWeekUseCase';
import { LoadTeachersUseCase } from '../../core/use-cases/LoadTeachersUseCase';

export function scheduleApp(
  loadGroupsUseCase: LoadGroupsUseCase,
  _loadScheduleUseCase: LoadScheduleWeekUseCase,
  loadTeachersUseCase: LoadTeachersUseCase
) {
  return {
    groups: [] as string[],
    teachers: [] as string[],
    selectedGroup: null as string | null,
    selectedTeacher: null as string | null,
    /** 'group' | 'teacher' */
    viewMode: 'group' as 'group' | 'teacher',
    error: null as string | null,
    loadingGroups: false,
    loadingTeachers: false,

    /** Строка поиска преподавателя в режиме выбора */
    teacherSearch: '',

    /** Показывать ли picker (поп-ап выбора группы/преподавателя) */
    showPicker: false,

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

      // Восстанавливаем из localStorage
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

    async loadGroups() {
      this.loadingGroups = true;
      this.error = null;
      try {
        const groups = await loadGroupsUseCase.execute();
        this.groups = Array.isArray(groups) ? groups : [];
      } catch (e) {
        console.error('loadGroups error', e);
        this.error = 'Не удалось загрузить список групп';
      } finally {
        this.loadingGroups = false;
      }
    },

    async loadTeachers() {
      if (this.teachers.length > 0) return;
      this.loadingTeachers = true;
      this.error = null;
      try {
        const teachers = await loadTeachersUseCase.execute();
        this.teachers = Array.isArray(teachers) ? teachers : [];
      } catch (e) {
        console.error('loadTeachers error', e);
        this.error = 'Не удалось загрузить список преподавателей';
      } finally {
        this.loadingTeachers = false;
      }
    },

    selectGroup(group: string) {
      // Сбрасываем значение в null, чтобы Alpine размонтировал компонент расписания
      this.selectedGroup = null;

      // Через 10мс устанавливаем новую группу. Alpine создаст свежий компонент расписания.
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

      // Сохраняем текущие значения
      const savedGroup = this.selectedGroup;
      const savedTeacher = this.selectedTeacher;

      // Принудительно сбрасываем, чтобы спровоцировать полное размонтирование
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