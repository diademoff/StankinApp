import Alpine from 'alpinejs';
import { ApiClient } from './infra/api/ApiClient';
import { LocalStorageCache } from './infra/cache/LocalStorageCache';
import { HttpGroupRepository } from './infra/repositories/HttpGroupRepository';
import { HttpScheduleRepository } from './infra/repositories/HttpScheduleRepository';
import { LoadGroupsUseCase } from './core/use-cases/LoadGroupsUseCase';
import { LoadScheduleWeekUseCase } from './core/use-cases/LoadScheduleWeekUseCase';
import { scheduleApp } from './ui/pages/scheduleApp';
import { scheduleComponent } from './ui/components/scheduleComponent';
import { teacherDiscussionApp } from './ui/pages/teacherDiscussionApp';
import { AuthRepository } from './core/ports/AuthRepository';
import { AuthWithYandexUseCase } from './core/use-cases/AuthWithYandexUseCase';
import { HttpAuthRepository } from './infra/repositories/HttpAuthRepository';

const api = new ApiClient('http://localhost:5000'); // Используйте ваш API_URL в проде
const cache = new LocalStorageCache();

const groupRepo = new HttpGroupRepository(api, cache);
const scheduleRepo = new HttpScheduleRepository(api, cache);

const loadGroupsUseCase = new LoadGroupsUseCase(groupRepo);
const loadScheduleUseCase = new LoadScheduleWeekUseCase(scheduleRepo);

const authRepo: AuthRepository = new HttpAuthRepository(api);
const authUseCase = new AuthWithYandexUseCase(authRepo);

document.addEventListener('alpine:init', () => {
  // @ts-ignore
  Alpine.data('scheduleApp', () => scheduleApp(loadGroupsUseCase, loadScheduleUseCase));
  // @ts-ignore
  Alpine.data('scheduleComponent', (groupName: string) =>
    scheduleComponent(groupName, loadScheduleUseCase, api)
  );
  // @ts-ignore
  Alpine.data('teacherDiscussionApp', () => teacherDiscussionApp(api, authUseCase));
});

console.log('Schedule application initialized');