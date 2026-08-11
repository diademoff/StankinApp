import './input.css';
import Alpine from 'alpinejs';
import { ApiClient } from './ApiClient';
import { scheduleApp } from './scheduleApp';
import { scheduleComponent } from './scheduleComponent';

// const api = new ApiClient('http://localhost:5000'); // debug only
const api   = new ApiClient('');

document.addEventListener('alpine:init', () => {
  const appInstance = scheduleApp(api);

  // @ts-ignore
  Alpine.data('scheduleApp', () => appInstance);
  // @ts-ignore
  Alpine.data('scheduleComponent', (subjectName: string, viewMode: 'group' | 'teacher') =>
    scheduleComponent(subjectName, viewMode, api)
  );
});

// @ts-ignore
window.Alpine = Alpine;

Alpine.start();
