import { loadEvents, saveEvents, getSetting, setSetting } from './storage.js';
import { renderDashboard } from './dashboard.js';
import { renderBrowse } from './browse.js';
import { renderReport } from './report.js';
import { renderSettings } from './settings.js';
import { initScoreboard, updateScoreboardEvents, setSyncStatus } from './scoreboard.js';
import { initAuth, onAuthChange } from './firebase-auth.js';
import { subscribeToEvents, unsubscribeEvents } from './firebase-sync.js';
import { subscribeToSettings, unsubscribeSettings } from './firebase-settings.js';
import { renderEntry } from './event-entry.js';
import { loadRoles, determineRole, getRole, canWrite, canManageUsers } from './roles.js';
import { calculateFeedingIntervals } from './calc.js';

let currentEvents = [];
let currentTab = 'dashboard';
let firebaseReady = false;

// ── Init ──
document.addEventListener('DOMContentLoaded', async () => {
  initTheme();
  initTabs();

  // Load cached data first (instant display)
  currentEvents = await loadEvents();
  calculateFeedingIntervals(currentEvents);
  initScoreboard(currentEvents, document.getElementById('scoreboard'));

  // Set title from baby name
  const babyName = getSetting('babyName', '');
  document.getElementById('app-title').textContent = babyName ? `주요 이벤트 일지 - ${babyName}` : '주요 이벤트 일지';

  switchTab('dashboard');

  // Initialize Firebase auth (non-blocking)
  await initFirebase();

  // Register Service Worker
  if ('serviceWorker' in navigator) {
    navigator.serviceWorker.register('./sw.js').catch(() => {});
  }

  // 네트워크 상태 감지
  window.addEventListener('online', () => setSyncStatus(true, new Date().toLocaleTimeString('ko-KR', { hour12: false })));
  window.addEventListener('offline', () => setSyncStatus(false));
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

    onAuthChange(async (user) => {
      if (user) {
        // Load roles and determine user's role
        await loadRoles(fb.db);
        const role = determineRole(user.email);

        if (!role) {
          // Not registered — show access denied
          document.querySelector('.tab-content').innerHTML =
            '<div class="empty-state" style="padding-top:60px;"><h2>접근 권한이 없습니다</h2><p>관리자에게 계정 등록을 요청하세요.</p><p style="color:var(--text-mid);margin-top:8px;">' + user.email + '</p></div>';
          updateTabVisibility(null);
          return;
        }

        updateTabVisibility(role);

        // All users share admin's data (UID from roles doc)
        const { getRolesData } = await import('./roles.js');
        const rolesData = getRolesData();
        const dataUid = rolesData?.dataUid || user.uid;
        subscribeToEvents(fb.db, dataUid, onFirestoreUpdate);
        subscribeToSettings(fb.db, dataUid);
        setSetting('firebaseUid', user.uid);
        setSetting('firebaseEmail', user.email);
        setSetting('userRole', role);
      } else {
        unsubscribeEvents();
        unsubscribeSettings();
        updateTabVisibility(null);
      }
      if (currentTab === 'settings') switchTab('settings');
    });
  } catch (err) {
    console.warn('Firebase init failed, using offline mode:', err);
  }
}

function onFirestoreUpdate(events) {
  currentEvents = events;
  calculateFeedingIntervals(currentEvents);
  setSyncStatus(true, new Date().toLocaleTimeString('ko-KR', { hour12: false }));
  updateScoreboardEvents(events);
  saveEvents(events);
  extractFormulaProducts(events);
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

function updateTabVisibility(role) {
  const entryTab = document.querySelector('nav button[data-tab="entry"]');
  const settingsTab = document.querySelector('nav button[data-tab="settings"]');

  if (!role) {
    // Not logged in — show all (offline mode)
    if (entryTab) entryTab.style.display = '';
    if (settingsTab) settingsTab.style.display = '';
    return;
  }

  if (role === 'observer') {
    if (entryTab) entryTab.style.display = 'none';
    if (settingsTab) settingsTab.style.display = 'none';
    // If currently on hidden tab, switch to dashboard
    if (currentTab === 'entry' || currentTab === 'settings') switchTab('dashboard');
  } else {
    if (entryTab) entryTab.style.display = '';
    if (settingsTab) settingsTab.style.display = '';
  }
}

function extractFormulaProducts(events) {
  const products = new Set(getSetting('formulaProducts', []));
  events.forEach(e => {
    if (e.formulaProduct && e.formulaProduct.trim()) products.add(e.formulaProduct.trim());
  });
  if (products.size > 0) {
    setSetting('formulaProducts', [...products]);
    if (!getSetting('defaultFormulaProduct')) {
      setSetting('defaultFormulaProduct', [...products][0]);
    }
  }
}

function onImport(events) {
  currentEvents = events;
  updateScoreboardEvents(events);
  const name = getSetting('babyName', '');
  document.getElementById('app-title').textContent = name ? `주요 이벤트 일지 - ${name}` : '주요 이벤트 일지';
}
