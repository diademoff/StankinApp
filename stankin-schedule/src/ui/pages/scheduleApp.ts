import { LoadGroupsUseCase } from '../../core/use-cases/LoadGroupsUseCase';
import { LoadScheduleWeekUseCase } from '../../core/use-cases/LoadScheduleWeekUseCase';

export function scheduleApp(
  loadGroupsUseCase: LoadGroupsUseCase,
  _loadScheduleUseCase: LoadScheduleWeekUseCase
) {
  return {
    groups: [] as string[],
    selectedGroup: null as string | null,
    error: null as string | null,
    loadingGroups: false,

    async init() {
      console.log('scheduleApp.init called');
      await this.loadGroups();
      const saved = localStorage.getItem('selectedGroup');
      if (saved) this.selectedGroup = saved;
    },

    async loadGroups() {
      console.log('scheduleApp.loadGroups called');
      this.loadingGroups = true;
      this.error = null;
      try {
        const groups = await loadGroupsUseCase.execute();
        console.log('groups from API:', groups);
        this.groups = Array.isArray(groups) ? groups : [];
      } catch (e) {
        console.error('loadGroups error', e);
        this.error = 'Не удалось загрузить список групп';
      } finally {
        this.loadingGroups = false;
      }
    },

    selectGroup(group: string) {
      this.selectedGroup = group;
      try { localStorage.setItem('selectedGroup', group); } catch {}
    },

    reset() {
      this.selectedGroup = null;
      try { localStorage.removeItem('selectedGroup'); } catch {}
    }
  };
}
