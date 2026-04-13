const CACHE_NAME = 'gichan-viewer-3.0.25';
const ASSETS = [
  './',
  './index.html',
  './css/style.css',
  './js/app.js',
  './js/calc.js',
  './js/excel-parser.js',
  './js/storage.js',
  './js/dashboard.js',
  './js/browse.js',
  './js/report.js',
  './js/settings.js',
  './js/scoreboard.js',
  './js/firebase-config.js',
  './js/firebase-auth.js',
  './js/firebase-sync.js',
  './js/event-entry.js',
  './js/firebase-settings.js',
  './js/roles.js',
  './lib/xlsx.mini.min.js',
  './lib/chart.umd.min.js',
  './manifest.json',
];

// 설치: 새 캐시 생성 후 즉시 활성화
self.addEventListener('install', (e) => {
  e.waitUntil(
    caches.open(CACHE_NAME)
      .then(cache => cache.addAll(ASSETS))
      .then(() => self.skipWaiting())
  );
});

// 활성화: 이전 캐시 삭제 + 즉시 클라이언트 점유
self.addEventListener('activate', (e) => {
  e.waitUntil(
    caches.keys().then(keys =>
      Promise.all(keys.filter(k => k !== CACHE_NAME).map(k => caches.delete(k)))
    ).then(() => self.clients.claim())
  );
});

// Network-first: 네트워크 우선, 실패 시 캐시 폴백 (GET만 캐시)
self.addEventListener('fetch', (e) => {
  if (e.request.method !== 'GET') return;
  e.respondWith(
    fetch(e.request)
      .then(response => {
        const clone = response.clone();
        caches.open(CACHE_NAME).then(cache => cache.put(e.request, clone));
        return response;
      })
      .catch(() => caches.match(e.request))
  );
});
