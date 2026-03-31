import { loadEvents, getSetting, setSetting } from './storage.js';
import { renderDashboard } from './dashboard.js';
import { renderBrowse } from './browse.js';
import { renderReport } from './report.js';
import { renderSettings } from './settings.js';
import { initScoreboard, updateScoreboardEvents } from './scoreboard.js';

let currentEvents = [];
let currentTab = 'dashboard';

// ── Init ──
document.addEventListener('DOMContentLoaded', async () => {
  initTheme();
  initTabs();
  currentEvents = await loadEvents();
  initScoreboard(currentEvents, document.getElementById('scoreboard'));
  switchTab('dashboard');

  // Register Service Worker
  if ('serviceWorker' in navigator) {
    navigator.serviceWorker.register('./sw.js').catch(() => {});
  }
});

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
    // Re-render current tab for chart color updates
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

function switchTab(tab) {
  currentTab = tab;

  // Update nav
  document.querySelectorAll('nav button').forEach(btn => {
    btn.classList.toggle('active', btn.dataset.tab === tab);
  });

  // Update pages
  document.querySelectorAll('.tab-page').forEach(page => {
    page.classList.toggle('active', page.id === `page-${tab}`);
  });

  // Render
  const container = document.getElementById(`page-${tab}`);
  switch (tab) {
    case 'dashboard': renderDashboard(currentEvents, container); break;
    case 'browse': renderBrowse(currentEvents, container); break;
    case 'report': renderReport(currentEvents, container); break;
    case 'settings': renderSettings(container, onImport); break;
  }
}

function onImport(events) {
  currentEvents = events;
  updateScoreboardEvents(events);
  const name = getSetting('babyName', '');
  document.getElementById('app-title').textContent = name ? `${name} 뷰어` : '기찬뷰어';
}
