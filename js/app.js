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
let isLoggedIn = false;

// ── Init ──
document.addEventListener('DOMContentLoaded', async () => {
  initTheme();
  initTabs();

  // 로그인 전: 데이터 표시 안 함 (전광판 숨김 + 모든 탭 로그인 안내)
  document.getElementById('scoreboard').style.display = 'none';
  document.getElementById('app-title').textContent = '주요 이벤트 일지';

  switchTab('dashboard');

  // Initialize Firebase auth (non-blocking)
  await initFirebase();

  // Register Service Worker + 강제 업데이트 체크
  if ('serviceWorker' in navigator) {
    navigator.serviceWorker.register('./sw.js').then(reg => {
      // 새 SW 발견 시 즉시 활성화 후 페이지 새로고침
      reg.addEventListener('updatefound', () => {
        const newSW = reg.installing;
        if (newSW) {
          newSW.addEventListener('statechange', () => {
            if (newSW.state === 'activated') {
              window.location.reload();
            }
          });
        }
      });
      // 수동 업데이트 체크
      reg.update();
    }).catch(() => {});
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
          isLoggedIn = false;
          document.getElementById('scoreboard').style.display = 'none';
          document.querySelector('.tab-content').innerHTML =
            '<div class="empty-state" style="padding-top:60px;"><h2>접근 권한이 없습니다</h2><p>관리자에게 계정 등록을 요청하세요.</p><p style="color:var(--text-mid);margin-top:8px;">' + user.email + '</p></div>';
          updateTabVisibility(null);
          return;
        }

        isLoggedIn = true;

        // 로그인 후: 캐시 로드 + 전광판 활성화
        currentEvents = await loadEvents();
        calculateFeedingIntervals(currentEvents);
        const sbEl = document.getElementById('scoreboard');
        sbEl.style.display = '';
        initScoreboard(currentEvents, sbEl);

        const babyName = getSetting('babyName', '');
        document.getElementById('app-title').textContent = babyName ? `주요 이벤트 일지 - ${babyName}` : '주요 이벤트 일지';

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

        switchTab(currentTab);
      } else {
        isLoggedIn = false;
        currentEvents = [];
        document.getElementById('scoreboard').style.display = 'none';
        document.getElementById('app-title').textContent = '주요 이벤트 일지';
        unsubscribeEvents();
        unsubscribeSettings();
        updateTabVisibility(null);
        switchTab(currentTab);
      }
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

export function switchTab(tab, payload = null) {
  currentTab = tab;

  document.querySelectorAll('nav button').forEach(btn => {
    btn.classList.toggle('active', btn.dataset.tab === tab);
  });

  document.querySelectorAll('.tab-page').forEach(page => {
    page.classList.toggle('active', page.id === `page-${tab}`);
  });

  const container = document.getElementById(`page-${tab}`);

  // 로그인 전: 데이터 표시 탭은 안내 메시지로 대체 (entry는 자체 처리, settings는 로그인 버튼 노출)
  if (!isLoggedIn && (tab === 'dashboard' || tab === 'browse' || tab === 'report')) {
    container.innerHTML = '<div class="empty-state" style="padding-top:60px;"><h2>로그인이 필요합니다</h2><p style="color:var(--text-mid);margin-top:8px;">설정 탭에서 로그인 후 이용해 주세요.</p></div>';
    return;
  }

  switch (tab) {
    case 'dashboard': renderDashboard(currentEvents, container); break;
    case 'entry': renderEntry(container, payload); break;
    case 'browse': renderBrowse(currentEvents, container, switchTab); break;
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
