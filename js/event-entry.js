// Event Entry — write new events to Firestore
import { getCurrentUser } from './firebase-auth.js';
import { getSetting, saveEventToLocal } from './storage.js';
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
  const defProduct = getSetting('defaultFormulaProduct') || products[0];
  const defFormula = getSetting('defaultFormulaAmount') || 100;
  const defBreast = getSetting('defaultBreastfeedAmount') || 20;

  switch (selectedCategory) {
    case '수유': {
      const formulaVal = editingEvent?.formulaAmount || defFormula;
      const breastVal = editingEvent?.breastfeedAmount || defBreast;
      const feedCountVal = editingEvent?.feedingCount || 1;
      const showBreastSetting = getSetting('showBreastfeed', false);
      // 숨김 설정이어도 수정 모드에서 기존 모유 데이터가 있으면 표시 (데이터 보호)
      const showBreast = showBreastSetting || (editingEvent?.breastfeedAmount > 0);
      // 버그2: 신규 입력은 분유 기본 체크, 수정 모드는 원래 분유량 유무에 따름
      const formulaChecked = !editingEvent || (editingEvent.formulaAmount > 0);
      // 수정 모드 + 모유량 있음 → 모유 체크 (화면 표시 될 때만 의미 있음)
      const breastChecked = editingEvent?.breastfeedAmount > 0;
      el.innerHTML = `
        <div class="entry-section">
          <div class="entry-row"><label>분유</label>
            <div class="toggle-row">
              <input type="checkbox" id="e-formula-on" ${formulaChecked ? 'checked' : ''}>
              <select id="e-formula-product">${products.map(p => `<option${p===defProduct?' selected':''}>${p}</option>`).join('')}</select>
              <div class="stepper">
                <button type="button" class="step-btn" data-target="e-formula-amt" data-step="-5">−</button>
                <span id="e-formula-display">${formulaVal}</span>
                <button type="button" class="step-btn" data-target="e-formula-amt" data-step="5">+</button>
              </div>
              <input type="hidden" id="e-formula-amt" value="${formulaVal}"> ml
            </div>
          </div>
          ${showBreast ? `<div class="entry-row"><label>모유</label>
            <div class="toggle-row">
              <input type="checkbox" id="e-breast-on" ${breastChecked ? 'checked' : ''}>
              <div class="stepper">
                <button type="button" class="step-btn" data-target="e-breast-amt" data-step="-5">−</button>
                <span id="e-breast-display">${breastVal}</span>
                <button type="button" class="step-btn" data-target="e-breast-amt" data-step="5">+</button>
              </div>
              <input type="hidden" id="e-breast-amt" value="${breastVal}"> ml
            </div>
          </div>` : '<input type="hidden" id="e-breast-on"><input type="hidden" id="e-breast-amt" value="0">'}
          <div class="entry-row"><label>분할 횟수</label>
            <div class="stepper">
              <button type="button" class="step-btn" data-target="e-feedcount" data-step="-1">−</button>
              <span id="e-feedcount-display">${feedCountVal}회</span>
              <button type="button" class="step-btn" data-target="e-feedcount" data-step="1">+</button>
            </div>
            <input type="hidden" id="e-feedcount" value="${feedCountVal}">
          </div>
        </div>`;
      // 스테퍼 버튼 이벤트
      el.querySelectorAll('.step-btn').forEach(btn => {
        btn.addEventListener('click', () => {
          const targetId = btn.dataset.target;
          const step = parseInt(btn.dataset.step);
          const input = document.getElementById(targetId);
          const limits = { 'e-formula-amt': [0, 300], 'e-breast-amt': [0, 100], 'e-feedcount': [1, 5] };
          const [min, max] = limits[targetId] || [0, 999];
          const newVal = Math.max(min, Math.min(max, parseInt(input.value) + step));
          input.value = newVal;
          const displayId = targetId === 'e-feedcount' ? 'e-feedcount-display' : targetId.replace('-amt', '-display');
          const displayEl = document.getElementById(displayId);
          if (displayEl) displayEl.textContent = targetId === 'e-feedcount' ? `${newVal}회` : newVal;
        });
      });
      if (editingEvent?.formulaProduct) {
        document.getElementById('e-formula-product').value = editingEvent.formulaProduct;
      }
      break;
    }

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
      // 수정 모드: detail에서 위생 항목 복원
      if (editingEvent && editingEvent.detail) {
        HYGIENE_TYPES.forEach(h => {
          if (editingEvent.detail.includes(h)) {
            const btn = document.querySelector(`#e-fields [data-hygiene="${h}"]`);
            if (btn) btn.classList.add('active');
          }
        });
      }
      break;

    case '신체측정':
      el.innerHTML = `
        <div class="entry-section">
          <div class="entry-row"><label>키 (cm)</label><input type="number" id="e-height" step="0.1" placeholder="0.0" style="width:80px"></div>
          <div class="entry-row"><label>몸무게 (kg)</label><input type="number" id="e-weight" step="0.01" placeholder="0.00" style="width:80px"></div>
          <div class="entry-row"><label>머리둘레 (cm)</label><input type="number" id="e-head" step="0.1" placeholder="0.0" style="width:80px"></div>
        </div>`;
      // 수정 모드: detail에서 수치 파싱 복원
      if (editingEvent && editingEvent.detail) {
        const hm = editingEvent.detail.match(/키\s*([\d.]+)/);
        const wm = editingEvent.detail.match(/몸무게\s*([\d.]+)/);
        const cm = editingEvent.detail.match(/머리둘레\s*([\d.]+)/);
        if (hm) document.getElementById('e-height').value = hm[1];
        if (wm) document.getElementById('e-weight').value = wm[1];
        if (cm) document.getElementById('e-head').value = cm[1];
      }
      break;

    case '건강관리':
      el.innerHTML = `
        <div class="entry-section">
          <div class="entry-row"><label>내용</label><input type="text" id="e-health" placeholder="증상/처치" style="flex:1"
            value="${editingEvent?.detail || ''}"></div>
        </div>`;
      break;

    case '기타':
      el.innerHTML = `
        <div class="entry-section">
          <div class="entry-row"><label>내용</label><input type="text" id="e-etc" placeholder="내용" style="flex:1"
            value="${editingEvent?.detail || ''}"></div>
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
    const { doc, setDoc, collection, Timestamp, deleteField } = await import('https://www.gstatic.com/firebasejs/10.12.0/firebase-firestore.js');
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
    data.dayNumber = evt.dayNumber ?? null;

    // 카테고리별 필드 정의
    const feedFields = ['formulaProduct','formulaAmount','breastfeedAmount','feedingCount','dailyFeedTotal'];
    const bowelFields = ['hasUrine','hasStool','immediateNotice'];

    // 수유 필드: 카테고리=수유면 값 반영, 아니면 Firestore에서 삭제
    if (evt.category === '수유') {
      feedFields.forEach(f => { data[f] = evt[f] ?? null; });
    } else {
      feedFields.forEach(f => { data[f] = deleteField(); });
    }

    // 배변 필드: 카테고리=배변이면 값 반영, 아니면 Firestore에서 삭제
    if (evt.category === '배변') {
      bowelFields.forEach(f => { data[f] = evt[f] ?? null; });
    } else {
      bowelFields.forEach(f => { data[f] = deleteField(); });
    }

    await setDoc(docRef, data, { merge: true });
    // meta/lastUpdated 갱신 (WPF 경량 폴링용)
    const metaRef = doc(db, 'users', dataUid, 'meta', 'lastUpdated');
    await setDoc(metaRef, { updatedAt: Timestamp.now(), source: 'pwa' }, { merge: true });
    status.textContent = editingEvent ? '✓ 수정 완료!' : '✓ 저장 완료!';

    if (!editingEvent) {
      // Reset time to now for next entry
      const now = new Date();
      document.getElementById('e-hour').value = now.getHours();
      document.getElementById('e-min').value = now.getMinutes();
    }
  } catch (err) {
    // Firestore 실패 시 IndexedDB에 로컬 저장
    try {
      const localData = { ...data, date: evt.fullDate, updatedAt: new Date() };
      if (localData.createdAt) localData.createdAt = new Date();
      await saveEventToLocal(localData);
      status.textContent = `⚠ 오프라인 저장됨 (동기화 대기)`;
      status.style.color = 'var(--cat-bowel)';
    } catch {
      status.textContent = `✗ 오류: ${err.message}`;
      status.style.color = 'var(--cat-health)';
    }
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

      const totalAmt = formulaAmt + breastAmt;
      base.amount = `${totalAmt}ml`;
      base.formulaProduct = formulaOn ? product : null;
      base.formulaAmount = formulaAmt > 0 ? formulaAmt : null;
      base.breastfeedAmount = breastAmt > 0 ? breastAmt : null;
      base.feedingCount = parseInt(document.getElementById('e-feedcount')?.value || 1);
      base.dailyFeedTotal = totalAmt > 0 ? totalAmt : null;
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
  const { doc, deleteDoc, setDoc, Timestamp } = await import('https://www.gstatic.com/firebasejs/10.12.0/firebase-firestore.js');
  const db = window.__firebase.db;
  const dataUid = getRolesData()?.dataUid || user.uid;
  await deleteDoc(doc(db, 'users', dataUid, 'events', eventId));
  // meta/lastUpdated 갱신 (WPF 경량 폴링용)
  await setDoc(doc(db, 'users', dataUid, 'meta', 'lastUpdated'), { updatedAt: Timestamp.now(), source: 'pwa' }, { merge: true });
  return true;
}

// ── Helpers ──
function range(n) { return Array.from({length: n}, (_, i) => i); }
function catShort(c) { return { '수유':'수유','배변':'배변','위생관리':'위생','신체측정':'신체','건강관리':'건강','기타':'기타' }[c] || c; }
function toISODate(d) {
  // 로컬 시각 기준 YYYY-MM-DD (toISOString은 UTC라 KST에서는 아침에 어제 날짜가 됨)
  if (!(d instanceof Date)) return String(d).slice(0,10);
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${y}-${m}-${day}`;
}
function setToggle(id, val) { const el = document.getElementById(id); if (el) { el.dataset.on = val.toString(); el.classList.toggle('active', val); } }
