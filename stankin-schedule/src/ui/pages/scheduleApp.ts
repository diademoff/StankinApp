import { LoadGroupsUseCase } from '../../core/use-cases/LoadGroupsUseCase';
import { LoadScheduleWeekUseCase } from '../../core/use-cases/LoadScheduleWeekUseCase';

export function scheduleApp(
  loadGroupsUseCase: LoadGroupsUseCase,
  loadScheduleUseCase: LoadScheduleWeekUseCase
) {
  return {
    groups: [] as string[],
    selectedGroup: null as string | null,
    loadingGroups: false,
    error: null as string | null,

    async init() {
      const saved = localStorage.getItem('selectedGroup');
      if (saved) {
        this.selectedGroup = saved;
      } else {
        await this.loadGroups();
      }
    },

    async loadGroups() {
      this.loadingGroups = true;
      try {
        this.groups = await loadGroupsUseCase.execute();
      } catch (e) {
        this.error = 'Не удалось загрузить группы';
        console.error(e);
      } finally {
        this.loadingGroups = false;
      }
    },

    selectGroup(group: string) {
      this.selectedGroup = group;
      localStorage.setItem('selectedGroup', group);
    },

    reset() {
      this.selectedGroup = null;
      localStorage.removeItem('selectedGroup');
    }
  };
}
