import Alpine from 'alpinejs';
import { ApiClient } from './infra/api/ApiClient';
import { LocalStorageCache } from './infra/cache/LocalStorageCache';
import { HttpGroupRepository } from './infra/repositories/HttpGroupRepository';
import { HttpScheduleRepository } from './infra/repositories/HttpScheduleRepository';
import { LoadGroupsUseCase } from './core/use-cases/LoadGroupsUseCase';
import { LoadScheduleWeekUseCase } from './core/use-cases/LoadScheduleWeekUseCase';
import { scheduleApp } from './ui/pages/scheduleApp';
import { scheduleComponent } from './ui/components/scheduleComponent';

// const api = new ApiClient('http://localhost:5000'); // debug only
const api   = new ApiClient('');
const cache = new LocalStorageCache();

const groupRepo    = new HttpGroupRepository(api, cache);
const scheduleRepo = new HttpScheduleRepository(api, cache);

const loadGroupsUseCase   = new LoadGroupsUseCase(groupRepo);
const loadScheduleUseCase = new LoadScheduleWeekUseCase(scheduleRepo);

document.addEventListener('alpine:init', () => {
  // @ts-ignore
  Alpine.data('scheduleApp', () => scheduleApp(loadGroupsUseCase, loadScheduleUseCase));
  // @ts-ignore
  Alpine.data('scheduleComponent', (groupName: string) =>
    scheduleComponent(groupName, loadScheduleUseCase)
  );
});

console.log('Schedule application initialized');