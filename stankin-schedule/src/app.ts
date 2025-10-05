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

// Функция для извлечения JWT из URL и сохранения в localStorage
const handleAuthCallback = () => {
  if (window.location.hash.includes('jwt=')) {
    const jwt = window.location.hash.split('jwt=')[1];
    localStorage.setItem('jwt_token', jwt);
    // Очищаем URL, чтобы токен не оставался в истории
    window.location.hash = '';
    history.replaceState(null, document.title, window.location.pathname + window.location.search);
  }
};

// Вызываем функцию при загрузке скрипта
handleAuthCallback();

const api = new ApiClient('http://localhost:5000'); // Используйте ваш API_URL в проде
const cache = new LocalStorageCache();

const groupRepo = new HttpGroupRepository(api, cache);
const scheduleRepo = new HttpScheduleRepository(api, cache);

const loadGroupsUseCase = new LoadGroupsUseCase(groupRepo);
const loadScheduleUseCase = new LoadScheduleWeekUseCase(scheduleRepo);

document.addEventListener('alpine:init', () => {
  // @ts-ignore
  Alpine.data('scheduleApp', () => scheduleApp(loadGroupsUseCase, loadScheduleUseCase));
  // @ts-ignore
  Alpine.data('scheduleComponent', (groupName: string) =>
    scheduleComponent(groupName, loadScheduleUseCase)
  );
  // @ts-ignore
  Alpine.data('teacherDiscussionApp', () => teacherDiscussionApp(api)); // Передаем ApiClient
});

// @ts-ignore
window.Alpine = Alpine;
Alpine.start();

console.log('Schedule application initialized');