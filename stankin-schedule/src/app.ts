import Alpine from 'alpinejs';
import { ApiClient } from './infra/api/ApiClient';
import { LocalStorageCache } from './infra/cache/LocalStorageCache';
import { scheduleApp } from './ui/pages/scheduleApp';
import { scheduleComponent } from './ui/components/scheduleComponent';

// const api = new ApiClient('http://localhost:5000'); // debug only
const api   = new ApiClient('');

const cache = new LocalStorageCache();

document.addEventListener('alpine:init', () => {
  const appInstance = scheduleApp(api, cache);

  // @ts-ignore
  Alpine.data('scheduleApp', () => appInstance);
  // @ts-ignore
  Alpine.data('scheduleComponent', (subjectName: string, viewMode: 'group' | 'teacher') =>
    scheduleComponent(subjectName, viewMode, api, cache)
  );
});

// @ts-ignore
window.Alpine = Alpine;

Alpine.start();
