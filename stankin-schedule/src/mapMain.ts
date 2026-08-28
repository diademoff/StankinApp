import Alpine from 'alpinejs';
import { mapApp } from './mapApp';

document.addEventListener('alpine:init', () => {
  // @ts-ignore
  Alpine.data('mapApp', () => mapApp());
});

// @ts-ignore
window.Alpine = Alpine;

Alpine.start();
