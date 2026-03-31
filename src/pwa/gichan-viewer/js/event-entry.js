// Event Entry — write new events to Firestore
import { getCurrentUser } from './firebase-auth.js';
import { getSetting } from './storage.js';
import { getRolesData } from './roles.js';

const CATEGORIES = ['수유', '배변', '위생관리', '신체측정', '건강관리', '기타'];
const HYGIENE_TYPES = ['샤워', '세안', '손발톱정리', '코청소', '눈꼽청소', '입안청소', '배꼽청소', '기타'];

let selectedCategory = '수유';
let editingEvent = null; // for edit mode

export function renderEntry(container, editEvent = null) {
  editingEvent = editEvent;
  const user = getCurrentUser();
  if (!user) {
    container.innerHTML = '<div class="empty-state">Google 로그인 후 사용 가능합니다.</div>';
    return;
  }

  const now = new Date();
  const dateVal = editEvent?.date ? toISODate(editEvent.date) : toISODate(now);
  const hourVal = editEvent?.time ? parseInt(editEvent.time.split(':')[0]) : now.getHours();
  const minVal = editEvent?.time ? parseInt(editEvent.time.split(':')[1]) : now.getMinutes();
  selectedCategory = editEvent?.category || '수유';

  container.innerHTML = `
    <div class="card">
      <div class="card-title cat-feed">${editEvent ? '기록 수정' : '기록 입력'}</div>

      <div class="entry-row">
        <label>날짜</label>
        <input type="date" id="e-date" value="${dateVal}">
      </div>
      <div class="entry-row">
        <label>시간</label>
        <div class="time-pick">
          <select id="e-hour">${range(24).map(h => `<option value="${h}" ${h===hourVal?'selected':''}>${String(h).padStart(2,'0')}</option>`).join('')}</select>
          <span>:</span>
          <select id="e-min">${range(60).map(m => `<option value="${m}" ${m===minVal?'selected':''}>${String(m).padStart(2,'0')}</option>`).join('')}</select>
        </div>
      </div>

      <div class="category-filters" id="e-cats">
        ${CATEGORIES.map(c => `<button class="cat-chip${c===selectedCategory?' active':''}" data-cat="${c}">${catShort(c)}</button>`).join('')}
      </div>

      <div id="e-fields"></div>

      <div class="entry-row">
        <label>메모</label>
        <input type="text" id="e-memo" value="${editEvent?.note || ''}" placeholder="비고">
      </div>

      <button class="import-btn" id="e-save">${editEvent ? '수정 완료' : '저장'}</button>
      <div id="e-status" style="text-align:center;margin-top:8px;font-weight:600;"></div>
    </div>`;

  renderCategoryFields();
  bindEntryEvents(container);
}

function renderCategoryFields() {
  const el = document.getElementById('e-fields');
  if (!el) return;

  const products = getSetting('formulaProducts') || ['트루맘 클래식'];
  const defFormula = getSetting('defaultFormulaAmount') || 100;
  const defBreast = getSetting('defaultBreastfeedAmount') || 20;

  switch (selectedCategory) {
    case '수유':
      el.innerHTML = `
        <div class="entry-section">
          <div class="entry-row"><label>분유</label>
            <div class="toggle-row">
              <input type="checkbox" id="e-formula-on" checked>
              <select id="e-formula-product">${products.map(p => `<option>${p}</option>`).join('')}</select>
              <input type="number" id="e-formula-amt" value="${editingEvent?.formulaAmount || defFormula}" min="0" max="300" step="5" style="width:70px"> ml
            </div>
          </div>
          <div class="entry-row"><label>모유</label>
            <div class="toggle-row">
              <input type="checkbox" id="e-breast-on">
              <input type="number" id="e-breast-amt" value="${editingEvent?.breastfeedAmount || defBreast}" min="0" max="100" step="5" style="width:70px"> ml
            </div>
          </div>
          <div class="entry-row"><label>분할 횟수</label>
            <select id="e-feedcount">${[1,2,3,4,5].map(n => `<option value="${n}" ${n===(editingEvent?.feedingCount||1)?'selected':''}>${n}회</option>`).join('')}</select>
          </div>
        </div>`;
      if (editingEvent) {
        if (editingEvent.formulaAmount > 0) document.getElementById('e-formula-on').checked = true;
        if (editingEvent.breastfeedAmount > 0) document.getElementById('e-breast-on').checked = true;
        if (editingEvent.formulaProduct) document.getElementById('e-formula-product').value = editingEvent.formulaProduct;
      }
      break;

    case '배변':
      el.innerHTML = `
        <div class="entry-section">
          <div class="toggle-row">
            <button class="cat-chip active" id="e-urine" data-on="true">소변</button>
            <button class="cat-chip" id="e-stool" data-on="false">대변</button>
            <button class="cat-chip" id="e-immediate" data-on="false">직후</button>
          </div>
        </div>`;
      if (editingEvent) {
        if (editingEvent.hasUrine) setToggle('e-urine', true);
        if (editingEvent.hasStool) setToggle('e-stool', true);
        if (editingEvent.immediateNotice) setToggle('e-immediate', true);
      }
      document.querySelectorAll('#e-fields .cat-chip').forEach(btn => {
        btn.addEventListener('click', () => {
          const on = btn.dataset.on === 'true';
          btn.dataset.on = (!on).toString();
          btn.classList.toggle('active', !on);
        });
      });
      break;

    case '위생관리':
      el.innerHTML = `
        <div class="entry-section">
          <div class="toggle-row" style="flex-wrap:wrap;gap:6px;">
            ${HYGIENE_TYPES.map(h => `<button class="cat-chip" data-hygiene="${h}">${h.replace('정리','').replace('청소','')}</button>`).join('')}
          </div>
        </div>`;
      document.querySelectorAll('#e-fields [data-hygiene]').forEach(btn => {
        btn.addEventListener('click', () => btn.classList.toggle('active'));
      });
      break;

    case '신체측정':
      el.innerHTML = `
        <div class="entry-section">
          <div class="entry-row"><label>키 (cm)</label><input type="number" id="e-height" step="0.1" placeholder="0.0" style="width:80px"></div>
          <div class="entry-row"><label>몸무게 (kg)</label><input type="number" id="e-weight" step="0.01" placeholder="0.00" style="width:80px"></div>
          <div class="entry-row"><label>머리둘레 (cm)</label><input type="number" id="e-head" step="0.1" placeholder="0.0" style="width:80px"></div>
        </div>`;
      break;

    case '건강관리':
      el.innerHTML = `
        <div class="entry-section">
          <div class="entry-row"><label>내용</label><input type="text" id="e-health" placeholder="증상/처치" style="flex:1"></div>
        </div>`;
      break;

    case '기타':
      el.innerHTML = `
        <div class="entry-section">
          <div class="entry-row"><label>내용</label><input type="text" id="e-etc" placeholder="내용" style="flex:1"></div>
        </div>`;
      break;
  }
}

function bindEntryEvents(container) {
  // Category selection
  container.querySelector('#e-cats').addEventListener('click', e => {
    const btn = e.target.closest('.cat-chip');
    if (!btn) return;
    selectedCategory = btn.dataset.cat;
    container.querySelectorAll('#e-cats .cat-chip').forEach(b => b.classList.remove('active'));
    btn.classList.add('active');
    renderCategoryFields();
  });

  // Save
  container.querySelector('#e-save').addEventListener('click', () => saveEvent(container));
}

async function saveEvent(container) {
  const status = container.querySelector('#e-status');
  const user = getCurrentUser();
  if (!user) { status.textContent = '로그인이 필요합니다.'; return; }

  status.textContent = '저장 중...';
  status.style.color = 'var(--cat-feed)';

  try {
    const evt = buildEvent();
    const { doc, setDoc, collection, Timestamp } = await import('https://www.gstatic.com/firebasejs/10.12.0/firebase-firestore.js');
    const db = window.__firebase.db;
    const docId = editingEvent?.id || crypto.randomUUID();
    const dataUid = getRolesData()?.dataUid || user.uid;
    const docRef = doc(db, 'users', dataUid, 'events', docId);

    const data = {
      id: docId,
      date: Timestamp.fromDate(evt.fullDate),
      time: evt.time,
      category: evt.category,
      detail: evt.detail,
      amount: evt.amount,
      note: evt.note,
      source: 'pwa',
      updatedAt: Timestamp.now(),
    };

    if (!editingEvent) data.createdAt = Timestamp.now();
    if (evt.dayNumber != null) data.dayNumber = evt.dayNumber;
    if (evt.formulaProduct) data.formulaProduct = evt.formulaProduct;
    if (evt.formulaAmount != null) data.formulaAmount = evt.formulaAmount;
    if (evt.breastfeedAmount != null) data.breastfeedAmount = evt.breastfeedAmount;
    if (evt.feedingCount != null) data.feedingCount = evt.feedingCount;
    if (evt.hasUrine != null) data.hasUrine = evt.hasUrine;
    if (evt.hasStool != null) data.hasStool = evt.hasStool;
    if (evt.immediateNotice != null) data.immediateNotice = evt.immediateNotice;

    await setDoc(docRef, data, { merge: true });
    status.textContent = editingEvent ? '✓ 수정 완료!' : '✓ 저장 완료!';

    if (!editingEvent) {
      // Reset time to now for next entry
      const now = new Date();
      document.getElementById('e-hour').value = now.getHours();
      document.getElementById('e-min').value = now.getMinutes();
    }
  } catch (err) {
    status.textContent = `✗ 오류: ${err.message}`;
    status.style.color = 'var(--cat-health)';
  }
}

function buildEvent() {
  const date = new Date(document.getElementById('e-date').value + 'T00:00:00');
  const hour = parseInt(document.getElementById('e-hour').value);
  const min = parseInt(document.getElementById('e-min').value);
  const fullDate = new Date(date); fullDate.setHours(hour, min, 0, 0);
  const time = `${String(hour).padStart(2,'0')}:${String(min).padStart(2,'0')}`;
  const note = document.getElementById('e-memo')?.value || '';

  const base = { fullDate, time, category: selectedCategory, note, detail: '', amount: '-' };

  switch (selectedCategory) {
    case '수유': {
      const formulaOn = document.getElementById('e-formula-on')?.checked;
      const breastOn = document.getElementById('e-breast-on')?.checked;
      const formulaAmt = formulaOn ? parseInt(document.getElementById('e-formula-amt')?.value || 0) : 0;
      const breastAmt = breastOn ? parseInt(document.getElementById('e-breast-amt')?.value || 0) : 0;
      const product = document.getElementById('e-formula-product')?.value || '분유';

      if (formulaAmt > 0 && breastAmt > 0) base.detail = `${product}+모유`;
      else if (breastAmt > 0) base.detail = '모유';
      else base.detail = product;

      base.amount = `${formulaAmt + breastAmt}ml`;
      base.formulaProduct = formulaOn ? product : null;
      base.formulaAmount = formulaAmt > 0 ? formulaAmt : null;
      base.breastfeedAmount = breastAmt > 0 ? breastAmt : null;
      base.feedingCount = parseInt(document.getElementById('e-feedcount')?.value || 1);
      break;
    }
    case '배변': {
      const hasUrine = document.getElementById('e-urine')?.dataset.on === 'true';
      const hasStool = document.getElementById('e-stool')?.dataset.on === 'true';
      const immediate = document.getElementById('e-immediate')?.dataset.on === 'true';
      const parts = [];
      if (hasUrine) parts.push('소변');
      if (hasStool) parts.push('대변');
      base.detail = parts.join('+');
      if (immediate) base.detail += '(직후)';
      base.hasUrine = hasUrine;
      base.hasStool = hasStool;
      base.immediateNotice = immediate;
      break;
    }
    case '위생관리': {
      const selected = [...document.querySelectorAll('#e-fields [data-hygiene].active')].map(b => b.dataset.hygiene);
      base.detail = selected.join(', ') || '위생관리';
      break;
    }
    case '신체측정': {
      const parts = [];
      const h = document.getElementById('e-height')?.value;
      const w = document.getElementById('e-weight')?.value;
      const hc = document.getElementById('e-head')?.value;
      if (h) parts.push(`키 ${h}cm`);
      if (w) parts.push(`몸무게 ${w}kg`);
      if (hc) parts.push(`머리둘레 ${hc}cm`);
      base.detail = parts.join(', ') || '신체측정';
      break;
    }
    case '건강관리':
      base.detail = document.getElementById('e-health')?.value || '건강관리';
      break;
    case '기타':
      base.detail = document.getElementById('e-etc')?.value || '기타';
      break;
  }

  return base;
}

// ── Delete event ──
export async function deleteEvent(eventId) {
  const user = getCurrentUser();
  if (!user || !eventId) return false;
  const { doc, deleteDoc } = await import('https://www.gstatic.com/firebasejs/10.12.0/firebase-firestore.js');
  const db = window.__firebase.db;
  const dataUid = getRolesData()?.dataUid || user.uid;
  await deleteDoc(doc(db, 'users', dataUid, 'events', eventId));
  return true;
}

// ── Helpers ──
function range(n) { return Array.from({length: n}, (_, i) => i); }
function catShort(c) { return { '수유':'수유','배변':'배변','위생관리':'위생','신체측정':'신체','건강관리':'건강','기타':'기타' }[c] || c; }
function toISODate(d) { return d instanceof Date ? d.toISOString().slice(0,10) : String(d).slice(0,10); }
function setToggle(id, val) { const el = document.getElementById(id); if (el) { el.dataset.on = val.toString(); el.classList.toggle('active', val); } }
