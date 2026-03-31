import * as C from './calc.js';
import { getSetting } from './storage.js';

const CATEGORIES = ['수유', '배변', '위생관리', '신체측정', '건강관리', '기타'];
let allEvents = [];
let activeCategories = new Set(CATEGORIES);
let startDate = '', endDate = '', keyword = '';
let currentPage = 1, sortOrder = 'desc';

export function renderBrowse(events, container) {
  allEvents = events;
  currentPage = 1;

  const today = new Date();
  const week = new Date(today); week.setDate(week.getDate() - 7);
  startDate = fmtISO(week);
  endDate = fmtISO(today);

  container.innerHTML = buildFilterUI() + '<div id="browse-list"></div><div id="browse-paging"></div>';
  bindEvents(container);
  applyFilter();
}

function buildFilterUI() {
  return `
    <div class="filter-bar">
      <input type="date" id="f-start" value="${startDate}">
      <input type="date" id="f-end" value="${endDate}">
      <input type="text" id="f-keyword" placeholder="키워드 검색">
      <button class="sort-btn" id="f-sort">최신순 ▼</button>
    </div>
    <div class="category-filters" id="cat-filters">
      ${CATEGORIES.map(c => `<button class="cat-chip active" data-cat="${c}">${catLabel(c)}</button>`).join('')}
    </div>`;
}

function bindEvents(container) {
  container.querySelector('#f-start').addEventListener('change', e => { startDate = e.target.value; currentPage = 1; applyFilter(); });
  container.querySelector('#f-end').addEventListener('change', e => { endDate = e.target.value; currentPage = 1; applyFilter(); });
  container.querySelector('#f-keyword').addEventListener('input', e => { keyword = e.target.value; currentPage = 1; applyFilter(); });
  container.querySelector('#f-sort').addEventListener('click', () => {
    sortOrder = sortOrder === 'desc' ? 'asc' : 'desc';
    document.getElementById('f-sort').textContent = sortOrder === 'desc' ? '최신순 ▼' : '오래된순 ▲';
    applyFilter();
  });

  container.querySelector('#cat-filters').addEventListener('click', e => {
    const btn = e.target.closest('.cat-chip');
    if (!btn) return;
    const cat = btn.dataset.cat;
    if (activeCategories.has(cat)) { activeCategories.delete(cat); btn.classList.remove('active'); }
    else { activeCategories.add(cat); btn.classList.add('active'); }
    currentPage = 1;
    applyFilter();
  });
}

function applyFilter() {
  const sd = startDate ? new Date(startDate + 'T00:00:00') : null;
  const ed = endDate ? new Date(endDate + 'T23:59:59') : null;
  const kw = keyword.toLowerCase();

  let filtered = allEvents.filter(e => {
    if (!activeCategories.has(e.category)) return false;
    if (sd && e.date < sd) return false;
    if (ed && e.date > ed) return false;
    if (kw && !`${e.detail} ${e.note} ${e.amount}`.toLowerCase().includes(kw)) return false;
    return true;
  });

  filtered.sort((a, b) => {
    const da = C.getFullDateTime(a) || a.date;
    const db = C.getFullDateTime(b) || b.date;
    return sortOrder === 'desc' ? db - da : da - db;
  });

  renderList(filtered);
}

function renderList(filtered) {
  const pageSize = getSetting('pageSize', 30);
  const totalPages = Math.max(1, Math.ceil(filtered.length / pageSize));
  if (currentPage > totalPages) currentPage = totalPages;

  const start = (currentPage - 1) * pageSize;
  const page = filtered.slice(start, start + pageSize);

  const listEl = document.getElementById('browse-list');
  if (!page.length) {
    listEl.innerHTML = '<div class="empty-state">조건에 맞는 기록이 없습니다.</div>';
  } else {
    listEl.innerHTML = page.map(buildEventCard).join('');
  }

  const pagingEl = document.getElementById('browse-paging');
  pagingEl.innerHTML = `
    <div class="pagination">
      <button id="pg-prev" ${currentPage <= 1 ? 'disabled' : ''}>◀ 이전</button>
      <span>${currentPage} / ${totalPages} (${filtered.length}건)</span>
      <button id="pg-next" ${currentPage >= totalPages ? 'disabled' : ''}>다음 ▶</button>
    </div>`;

  pagingEl.querySelector('#pg-prev')?.addEventListener('click', () => { if (currentPage > 1) { currentPage--; renderList(filtered); } });
  pagingEl.querySelector('#pg-next')?.addEventListener('click', () => { if (currentPage < totalPages) { currentPage++; renderList(filtered); } });
}

function buildEventCard(evt) {
  const dt = C.getFullDateTime(evt);
  const dateStr = dt ? `${String(dt.getMonth()+1).padStart(2,'0')}/${String(dt.getDate()).padStart(2,'0')}` : '';
  const timeStr = evt.time || '';
  const catClass = C.getCategoryClass(evt);
  const detailClass = C.getCategoryClass(evt);

  return `<div class="event-card">
    <div class="ec-header">
      <span class="ec-time">${dateStr} ${timeStr}</span>
      <span class="ec-category ${catClass}">${catLabel(evt.category)}</span>
    </div>
    <div class="ec-detail ${detailClass}">${evt.detail}</div>
    <div class="ec-amount">${evt.amount}${evt.note ? ` · ${evt.note}` : ''}</div>
  </div>`;
}

function catLabel(cat) {
  const map = { '수유': '수유', '배변': '배변', '위생관리': '위생', '신체측정': '신체', '건강관리': '건강', '기타': '기타' };
  return map[cat] || cat;
}

function fmtISO(d) {
  return d.toISOString().slice(0, 10);
}
