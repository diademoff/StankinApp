import Alpine from 'alpinejs';
import { ApiClient } from './infra/api/ApiClient';
import { LocalStorageCache } from './infra/cache/LocalStorageCache';
import { HttpGroupRepository } from './infra/repositories/HttpGroupRepository';
import { HttpScheduleRepository } from './infra/repositories/HttpScheduleRepository';
import { LoadGroupsUseCase } from './core/use-cases/LoadGroupsUseCase';
import { LoadScheduleWeekUseCase } from './core/use-cases/LoadScheduleWeekUseCase';
import { LoadTeacherScheduleWeekUseCase } from './core/use-cases/LoadTeacherScheduleWeekUseCase';
import { LoadTeachersUseCase } from './core/use-cases/LoadTeachersUseCase';
import { scheduleApp } from './ui/pages/scheduleApp';
import { scheduleComponent } from './ui/components/scheduleComponent';

const api = new ApiClient('http://localhost:5000'); // debug only
// const api   = new ApiClient('');
const cache = new LocalStorageCache();

const groupRepo    = new HttpGroupRepository(api, cache);
const scheduleRepo = new HttpScheduleRepository(api, cache);

const loadGroupsUseCase              = new LoadGroupsUseCase(groupRepo);
const loadTeachersUseCase            = new LoadTeachersUseCase(groupRepo);
const loadScheduleUseCase            = new LoadScheduleWeekUseCase(scheduleRepo);
const loadTeacherScheduleUseCase     = new LoadTeacherScheduleWeekUseCase(scheduleRepo);

document.addEventListener('alpine:init', () => {
  const appInstance = scheduleApp(
    loadGroupsUseCase,
    loadScheduleUseCase,
    loadTeachersUseCase
  );

  // @ts-ignore
  Alpine.data('scheduleApp', () => appInstance);
  // @ts-ignore
  Alpine.data('scheduleComponent', (subjectName: string, viewMode: 'group' | 'teacher') =>
    scheduleComponent(subjectName, viewMode, loadScheduleUseCase, loadTeacherScheduleUseCase)
  );
});

// @ts-ignore
window.Alpine = Alpine;

Alpine.start();

console.log('Schedule application initialized');