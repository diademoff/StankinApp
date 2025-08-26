import { precacheAndRoute } from 'workbox-precaching';

const CACHE_NAME = "alpine-cache-v1";
const API_CACHE = "api-cache-v1";

precacheAndRoute(self.__WB_MANIFEST || []);

self.addEventListener("install", e => {
  console.log("SW installing");
});

self.addEventListener("activate", e => {
  console.log("SW activated");
});

self.addEventListener("fetch", e => {
  const req = e.request;
  const url = new URL(e.request.url);

  if (
    url.origin.includes("localhost:5173") &&
    (url.pathname.startsWith("/@vite") ||
     url.pathname.startsWith("/src/") ||
     url.pathname.startsWith("/public/"))
  ) {
    e.respondWith(fetch(e.request));
    return;
  }

  if (url.origin === self.location.origin && url.pathname.startsWith('/assets/')) {
    e.respondWith((async () => {
      const cache = await caches.open(CACHE_NAME);
      const cached = await cache.match(req);
      if (cached) return cached;

      try {
        const resp = await fetch(req);
        if (resp.ok) {
          cache.put(req, resp.clone());
        }
        return resp;
      } catch (error) {
        // Fallback для оффлайн: верните сообщение или offline-страницу
        return new Response('Приложение работает в оффлайн-режиме, но этот ресурс недоступен без сети.', {
          headers: { 'Content-Type': 'text/plain' }
        });
      }
    })());
    return;
  }

  e.respondWith(
    caches.match(e.request).then(r => r || fetch(e.request))
  );
});

console.log("SW registered");
