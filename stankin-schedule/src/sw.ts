/// <reference lib="webworker" />
import { precacheAndRoute } from 'workbox-precaching';
import { registerRoute } from 'workbox-routing';
import { NetworkFirst, StaleWhileRevalidate, CacheFirst } from 'workbox-strategies';
import { CacheableResponsePlugin } from 'workbox-cacheable-response';
import { CacheExpiration, ExpirationPlugin } from 'workbox-expiration';

declare const self: ServiceWorkerGlobalScope;

// ⚡ precache всех файлов, которые vite-plugin-pwa сюда подставит
precacheAndRoute(self.__WB_MANIFEST || []);

// ============================================================================
// Расписание и списки: мгновенно из кэша → фоновая актуализация → live-обновление
// ============================================================================
// Приоритет: свежесть не критична (БД пересобирается раз в день), но офлайн/падение
// api должно отдавать ранее загруженное расписание мгновенно. Поэтому схема:
//   1) ответ сразу из кэша;
//   2) в фоне сетевой fetch;
//   3) при реальном изменении тела — обновляем кэш и шлём SCHEDULE_UPDATED,
//      страница перерисовывает открытую неделю без рефетча.
const SCHEDULE_CACHE = 'schedule-cache-v1';
const scheduleExpiration = new CacheExpiration(SCHEDULE_CACHE, {
  maxEntries: 600,
  maxAgeSeconds: 30 * 24 * 60 * 60,
});

function isScheduleRead(pathname: string): boolean {
  if (
    pathname.startsWith('/api/groups') ||
    pathname.startsWith('/api/rooms') ||
    pathname.startsWith('/api/schedule')
  ) return true;
  // ровно список преподавателей; /api/teachers/validate — мимо (запросы по имени)
  return pathname === '/api/teachers';
}

registerRoute(
  ({ url, request }) => request.method === 'GET' && isScheduleRead(url.pathname),
  async ({ request, url }) => {
    const cache = await caches.open(SCHEDULE_CACHE);
    const cached = await cache.match(request);
    if (cached) {
      // показываем из кэша мгновенно, актуализацию гоняем в фоне
      void revalidate(request, url, cache);
      return cached;
    }
    try {
      const resp = await fetch(request);
      if (resp.ok) {
        await cache.put(request, resp.clone());
        await scheduleExpiration.updateTimestamp(request.url);
        await scheduleExpiration.expireEntries();
      }
      return resp;
    } catch {
      // кэша нет и сеть недоступна — данных никогда не было, пробрасываем ошибку
      throw new Error('offline: no cached response');
    }
  }
);

async function revalidate(request: Request, url: URL, cache: Cache): Promise<void> {
  try {
    const fresh = await fetch(request);
    if (!fresh.ok) return;

    const freshText = await fresh.clone().text();
    const old = await cache.match(request);
    if (old) {
      const oldText = await old.text();
      if (oldText === freshText) {
        // данные не изменились — обновляем только timestamp давности
        await scheduleExpiration.updateTimestamp(request.url);
        return;
      }
    }

    await cache.put(request, fresh);
    await scheduleExpiration.updateTimestamp(request.url);
    await scheduleExpiration.expireEntries();

    const clients = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
    for (const client of clients) {
      client.postMessage({ type: 'SCHEDULE_UPDATED', url: url.href, data: freshText });
    }
  } catch {
    // фоновая актуализация упала (офлайн/502) — молча: пользователь уже получил кэш
  }
}

// ⚡ кеш для api (остальное: доска, вьюализация и т.п.) — сеть важнее кэша
registerRoute(
  ({ url }) => url.pathname.startsWith('/api/'),
  new NetworkFirst({
    cacheName: 'api-cache',
    plugins: [
      new CacheableResponsePlugin({ statuses: [0, 200] }),
      new ExpirationPlugin({ maxEntries: 100, maxAgeSeconds: 60 * 60 * 8 }) // 8 часов
    ],
  })
);

// ⚡ кеш для изображений/иконок
registerRoute(
  ({ request }) => request.destination === 'image',
  new CacheFirst({
    cacheName: 'image-cache',
    plugins: [
      new CacheableResponsePlugin({ statuses: [0, 200] }),
      new ExpirationPlugin({ maxEntries: 50, maxAgeSeconds: 7 * 24 * 60 * 60 }) // 1 неделя
    ],
  })
);

// ⚡ кеш для статики (css, js)
registerRoute(
  ({ request }) => request.destination === 'script' || request.destination === 'style',
  new StaleWhileRevalidate({
    cacheName: 'static-resources',
  })
);

self.addEventListener('message', (event) => {
  if (event.data && event.data.type === 'SKIP_WAITING') {
    self.skipWaiting();
  }
});

console.log('Custom Service Worker loaded');
