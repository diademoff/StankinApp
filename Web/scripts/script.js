import { scheduleApp } from './scheduleApp.js';
import { scheduleComponent } from './scheduleComponent.js';

document.addEventListener('alpine:init', () => {
    Alpine.data('scheduleApp', scheduleApp);
    Alpine.data('scheduleComponent', (groupNameInitial) => scheduleComponent(groupNameInitial));
});

console.log('Schedule application initialized');