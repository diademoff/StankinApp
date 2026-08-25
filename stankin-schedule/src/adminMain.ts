import Alpine from 'alpinejs';
import { ApiClient } from './ApiClient';
import { adminApp } from './adminApp';

// const api = new ApiClient('http://localhost:5000'); // debug only
const api = new ApiClient('');

document.addEventListener('alpine:init', () => {
  // @ts-ignore
  Alpine.data('adminApp', () => adminApp(api));
});

// @ts-ignore
window.Alpine = Alpine;

Alpine.start();
