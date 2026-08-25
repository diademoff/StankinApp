import Alpine from 'alpinejs';
import { ApiClient } from './ApiClient';
import { boardApp } from './boardApp';

// const api = new ApiClient('http://localhost:5000'); // debug only
const api = new ApiClient('');

document.addEventListener('alpine:init', () => {
  // @ts-ignore
  Alpine.data('boardApp', () => boardApp(api));
});

// @ts-ignore
window.Alpine = Alpine;

Alpine.start();
