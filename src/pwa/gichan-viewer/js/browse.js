import * as C from './calc.js';
import { getSetting } from './storage.js';
import { getCurrentUser } from './firebase-auth.js';
import { getRolesData, canWrite } from './roles.js';
import { renderEntry } from './event-entry.js';

const CATEGORIES = ['수유', '배변', '위생관리', '신체측정', '건강관리', '기타'];
let allEvents = [];
let activeCategories = new Set(CATEGORIES);
let startDate = '', endDate = '', keyword = '';
let currentPage = 1, sortOrder = 'desc';
let tabSwitchCallback = null;

export function renderBrowse(events, container, onTabSwitch = null) {
  allEvents = events;
  currentPage = 1;
  tabSwitchCallback = onTabSwitch;

  const today = new Date();
  const week = new Date(today); week.setDate(week.getDate() - 7);
  startDate = fmtISO(week);
  endDate = fmtISO(today);

  container.innerHTML = '<div id="browse-filters" class="browse-filters-sticky">' + buildFilterUI() + '</div><div id="browse-list"></div><div id="browse-paging"></div>';
  bindEvents(container);
  applyFilter();
}

function buildFilterUI() {
  return `
    <div class="filter-bar">
      <input type="date" id="f-start" value="${startDate}" style="flex:1;">
      <input type="date" id="f-end" value="${endDate}" style="flex:1;">
    </div>
    <div class="filter-bar">
      <input type="text" id="f-keyword" placeholder="키워드 검색" style="flex:1;">
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
  const writable = canWrite();
  if (!page.length) {
    listEl.innerHTML = '<div class="empty-state">조건에 맞는 기록이 없습니다.</div>';
  } else {
    listEl.innerHTML = page.map((evt, i) => buildEventCard(evt, start + i, writable)).join('');
    // 수정/삭제 버튼 이벤트 바인딩
    listEl.querySelectorAll('.ec-edit-btn').forEach(btn => {
      btn.addEventListener('click', (e) => {
        e.stopPropagation();
        const idx = parseInt(btn.dataset.idx);
        const evt = filtered[idx];
        if (evt && tabSwitchCallback) tabSwitchCallback('entry', evt);
      });
    });
    listEl.querySelectorAll('.ec-del-btn').forEach(btn => {
      btn.addEventListener('click', async (e) => {
        e.stopPropagation();
        const idx = parseInt(btn.dataset.idx);
        const evt = filtered[idx];
        if (!evt) return;
        const dt = C.getFullDateTime(evt);
        const dateStr = dt ? `${dt.getFullYear()}/${String(dt.getMonth()+1).padStart(2,'0')}/${String(dt.getDate()).padStart(2,'0')} ${evt.time || ''}` : '';
        if (!confirm(`${dateStr} ${evt.detail} 기록을 삭제하시겠습니까?`)) return;
        try {
          await deleteEventFromFirestore(evt);
          // 실시간 리스너가 갱신하니 UI 업데이트는 자동으로 됨
        } catch (err) {
          alert(`삭제 실패: ${err.message}`);
        }
      });
    });
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

function buildEventCard(evt, idx, writable) {
  const dt = C.getFullDateTime(evt);
  const dateStr = dt ? `${String(dt.getMonth()+1).padStart(2,'0')}/${String(dt.getDate()).padStart(2,'0')}` : '';
  const timeStr = evt.time || '';
  const catClass = C.getCategoryClass(evt);
  const detailClass = C.getCategoryClass(evt);
  const actions = writable ? `
    <div class="ec-actions">
      <button class="ec-edit-btn" data-idx="${idx}" title="수정">✎</button>
      <button class="ec-del-btn" data-idx="${idx}" title="삭제">✕</button>
    </div>` : '';

  return `<div class="event-card">
    <div class="ec-header">
      <span class="ec-time">${dateStr} ${timeStr}</span>
      <span class="ec-category ${catClass}">${catLabel(evt.category)}</span>
      ${actions}
    </div>
    <div class="ec-detail ${detailClass}">${evt.detail}</div>
    <div class="ec-amount">${evt.amount}${evt.feedingInterval ? ` · 수유텀 ${evt.feedingInterval}` : ''}${evt.note ? ` · ${evt.note}` : ''}</div>
  </div>`;
}

async function deleteEventFromFirestore(evt) {
  const { doc, deleteDoc, setDoc, Timestamp } = await import('https://www.gstatic.com/firebasejs/10.12.0/firebase-firestore.js');
  const db = window.__firebase.db;
  const user = getCurrentUser();
  if (!user) throw new Error('로그인이 필요합니다.');
  const dataUid = getRolesData()?.dataUid || user.uid;
  const docId = evt.id;
  if (!docId) throw new Error('이벤트 ID가 없습니다.');
  await deleteDoc(doc(db, 'users', dataUid, 'events', docId));
  // meta/lastUpdated 갱신 (다른 기기 동기화 알림)
  try {
    await setDoc(doc(db, 'users', dataUid, 'meta', 'lastUpdated'), {
      updatedAt: Timestamp.now(), source: 'pwa'
    });
  } catch { /* 메타 업데이트 실패는 무시 */ }
}

function catLabel(cat) {
  const map = { '수유': '수유', '배변': '배변', '위생관리': '위생', '신체측정': '신체', '건강관리': '건강', '기타': '기타' };
  return map[cat] || cat;
}

function fmtISO(d) {
  return d.toISOString().slice(0, 10);
}
