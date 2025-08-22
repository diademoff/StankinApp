import scheduleApp from './scheduleApp.js';
import scheduleComponent from './scheduleComponent.js';


import './input.css';
import Alpine from 'alpinejs';
window.Alpine = Alpine;

document.addEventListener('alpine:init', () => {
    Alpine.data('scheduleApp', scheduleApp);
    Alpine.data('scheduleComponent', (groupNameInitial) => scheduleComponent(groupNameInitial));
});

Alpine.start();

console.log('Schedule application initialized');

if ('serviceWorker' in navigator) {
  window.addEventListener('load', () => {
    navigator.serviceWorker.register('/sw.js')
      .then(reg => console.log("SW registered", reg))
      .catch(err => console.error("SW failed", err));
  });
}
