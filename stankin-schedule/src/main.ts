import './app';
import './input.css';
import Alpine from 'alpinejs';
import { scheduleApp } from './ui/pages/scheduleApp';
import { scheduleComponent } from './ui/components/scheduleComponent';
// @ts-ignore
window.Alpine = Alpine;
// TODO:
// Alpine.data('scheduleApp', () => scheduleApp(...deps));
// Alpine.data('scheduleComponent', (groupName) => scheduleComponent(groupName, ...deps));
Alpine.start();
