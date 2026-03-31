import { loadEvents, saveEvents, getSetting, setSetting } from './storage.js';
import { renderDashboard } from './dashboard.js';
import { renderBrowse } from './browse.js';
import { renderReport } from './report.js';
import { renderSettings } from './settings.js';
import { initScoreboard, updateScoreboardEvents } from './scoreboard.js';
import { initAuth, onAuthChange } from './firebase-auth.js';
import { subscribeToEvents, unsubscribeEvents } from './firebase-sync.js';
import { renderEntry } from './event-entry.js';

let currentEvents = [];
let currentTab = 'dashboard';
let firebaseReady = false;

// ── Init ──
document.addEventListener('DOMContentLoaded', async () => {
  initTheme();
  initTabs();

  // Load cached data first (instant display)
  currentEvents = await loadEvents();
  initScoreboard(currentEvents, document.getElementById('scoreboard'));
  switchTab('dashboard');

  // Initialize Firebase auth (non-blocking)
  await initFirebase();

  // Register Service Worker
  if ('serviceWorker' in navigator) {
    navigator.serviceWorker.register('./sw.js').catch(() => {});
  }
});

// ── Firebase Init ──
async function initFirebase() {
  // Wait for Firebase SDK to load
  const waitForFirebase = () => new Promise((resolve) => {
    const check = () => window.__firebase ? resolve(window.__firebase) : setTimeout(check, 100);
    check();
  });

  try {
    const fb = await waitForFirebase();
    await initAuth(fb.auth);
    firebaseReady = true;

    onAuthChange((user) => {
      if (user) {
        // Signed in → subscribe to Firestore
        subscribeToEvents(fb.db, user.uid, onFirestoreUpdate);
        setSetting('firebaseUid', user.uid);
        setSetting('firebaseEmail', user.email);
      } else {
        // Signed out → unsubscribe
        unsubscribeEvents();
      }
      // Re-render settings tab if visible
      if (currentTab === 'settings') switchTab('settings');
    });
  } catch (err) {
    console.warn('Firebase init failed, using offline mode:', err);
  }
}

function onFirestoreUpdate(events) {
  currentEvents = events;
  updateScoreboardEvents(events);
  // Cache to IndexedDB for offline use
  saveEvents(events);
  // Re-render current tab
  const container = document.getElementById(`page-${currentTab}`);
  if (container) {
    switch (currentTab) {
      case 'dashboard': renderDashboard(currentEvents, container); break;
      case 'browse': renderBrowse(currentEvents, container); break;
      case 'report': renderReport(currentEvents, container); break;
    }
  }
}

// ── Theme ──
function initTheme() {
  const theme = getSetting('theme', 'dark');
  document.documentElement.setAttribute('data-theme', theme);
  updateThemeButton(theme);

  document.getElementById('theme-toggle').addEventListener('click', () => {
    const current = document.documentElement.getAttribute('data-theme');
    const next = current === 'dark' ? 'light' : 'dark';
    document.documentElement.setAttribute('data-theme', next);
    setSetting('theme', next);
    updateThemeButton(next);
    if (currentTab === 'report') switchTab('report');
  });
}

function updateThemeButton(theme) {
  document.getElementById('theme-toggle').textContent = theme === 'dark' ? '☀️' : '🌙';
}

// ── Tab Navigation ──
function initTabs() {
  document.querySelectorAll('nav button').forEach(btn => {
    btn.addEventListener('click', () => switchTab(btn.dataset.tab));
  });
}

export function switchTab(tab) {
  currentTab = tab;

  document.querySelectorAll('nav button').forEach(btn => {
    btn.classList.toggle('active', btn.dataset.tab === tab);
  });

  document.querySelectorAll('.tab-page').forEach(page => {
    page.classList.toggle('active', page.id === `page-${tab}`);
  });

  const container = document.getElementById(`page-${tab}`);
  switch (tab) {
    case 'dashboard': renderDashboard(currentEvents, container); break;
    case 'entry': renderEntry(container); break;
    case 'browse': renderBrowse(currentEvents, container); break;
    case 'report': renderReport(currentEvents, container); break;
    case 'settings': renderSettings(container, onImport, firebaseReady); break;
  }
}

function onImport(events) {
  currentEvents = events;
  updateScoreboardEvents(events);
  const name = getSetting('babyName', '');
  document.getElementById('app-title').textContent = name ? `${name} 뷰어` : '기찬뷰어';
}
