const CACHE_NAME = 'gichan-viewer-v13-feedback';
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
  './lib/xlsx.mini.min.js',
  './lib/chart.umd.min.js',
  './manifest.json',
];

self.addEventListener('install', (e) => {
  e.waitUntil(
    caches.open(CACHE_NAME)
      .then(cache => cache.addAll(ASSETS))
      .then(() => self.skipWaiting())
  );
});

self.addEventListener('activate', (e) => {
  e.waitUntil(
    caches.keys().then(keys =>
      Promise.all(keys.filter(k => k !== CACHE_NAME).map(k => caches.delete(k)))
    ).then(() => self.clients.claim())
  );
});

self.addEventListener('fetch', (e) => {
  e.respondWith(
    caches.match(e.request).then(cached => cached || fetch(e.request))
  );
});
