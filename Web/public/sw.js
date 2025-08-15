const CACHE_NAME = "alpine-cache-v1";
const API_CACHE = "api-cache-v1";
const STATIC_ASSETS = ["/", "/index.html", "/script.js"];

self.addEventListener("install", e => {
  e.waitUntil(
    caches.open(CACHE_NAME).then(cache => cache.addAll(STATIC_ASSETS))
  );
});

self.addEventListener("fetch", e => {
  const url = new URL(e.request.url);

  if (url.origin === location.origin) {
    // Cache First для статики
    e.respondWith(
      caches.match(e.request).then(res => res || fetch(e.request))
    );
  } else if (url.pathname.startsWith("/api/")) {
    // Network First для API
    e.respondWith(
      fetch(e.request)
        .then(res => {
          const resClone = res.clone();
          caches.open(API_CACHE).then(cache => cache.put(e.request, resClone));
          return res;
        })
        .catch(() => caches.match(e.request))
    );
  }
});
