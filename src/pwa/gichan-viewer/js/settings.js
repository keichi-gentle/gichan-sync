import { parseExcelFile } from './excel-parser.js';
import { saveEvents, getSetting, setSetting } from './storage.js';
import { signIn, signOut, getCurrentUser } from './firebase-auth.js';
import { saveSettingsToFirestore, getDefaultSettings } from './firebase-settings.js';

let onImportCallback = null;

export function renderSettings(container, onImport, firebaseReady = false) {
  onImportCallback = onImport;

  const lastDate = getSetting('lastImportDate');
  const lastCount = getSetting('lastImportCount', 0);
  const pageSize = getSetting('pageSize', 30);
  const babyName = getSetting('babyName', '');
  const importInfo = lastDate
    ? `마지막 가져오기: ${new Date(lastDate).toLocaleString('ko-KR')} (${lastCount}건)`
    : '아직 가져온 파일이 없습니다.';

  const user = getCurrentUser();
  const authSection = firebaseReady ? buildAuthSection(user) : '<div class="import-info">Firebase 연결 중...</div>';

  container.innerHTML = `
    <div class="setting-group">
      <h3>클라우드 동기화</h3>
      <div id="auth-section">${authSection}</div>
    </div>

    <div class="setting-group">
      <h3>데이터 관리</h3>
      <button class="import-btn" id="import-btn" style="background:var(--cat-etc);">📂 Excel 파일 가져오기 (수동)</button>
      <input type="file" id="file-input" accept=".xlsx,.xls" style="display:none">
      <div class="import-info" id="import-info">${importInfo}</div>
      <div id="import-status" style="text-align:center;margin-top:8px;color:var(--cat-feed);font-weight:600;"></div>
    </div>

    <div class="setting-group">
      <h3>표시 설정</h3>
      <div class="setting-row">
        <label>페이지당 표시 건수</label>
        <select id="set-pagesize">
          ${[10,20,30,50,100].map(n => `<option value="${n}" ${n===pageSize?'selected':''}>${n}건</option>`).join('')}
        </select>
      </div>
      <div class="setting-row">
        <label>아기 이름</label>
        <input type="text" id="set-babyname" value="${babyName}" placeholder="이름 입력" style="width:120px;text-align:right;">
      </div>
    </div>

    <div class="setting-group">
      <h3>수유 설정</h3>
      <div class="setting-row">
        <label>고정 수유텀</label>
        <div class="stepper">
          <button class="step-btn" data-target="set-interval" data-step="-0.5">−</button>
          <span id="set-interval-display">${(getSetting('fixedFeedingInterval', 10800) / 3600).toFixed(1)}시간</span>
          <input type="hidden" id="set-interval" value="${(getSetting('fixedFeedingInterval', 10800) / 3600).toFixed(1)}">
          <button class="step-btn" data-target="set-interval" data-step="0.5">+</button>
        </div>
      </div>
      <div class="setting-row">
        <label>평균 계산 횟수</label>
        <div class="stepper">
          <button class="step-btn" data-target="set-avgcount" data-step="-1">−</button>
          <span id="set-avgcount-display">${getSetting('averageFeedingCount', 10)}회</span>
          <input type="hidden" id="set-avgcount" value="${getSetting('averageFeedingCount', 10)}">
          <button class="step-btn" data-target="set-avgcount" data-step="1">+</button>
        </div>
      </div>
      <div class="setting-row">
        <label>기본 분유량</label>
        <div class="stepper">
          <button class="step-btn" data-target="set-defformula" data-step="-5">−</button>
          <span id="set-defformula-display">${getSetting('defaultFormulaAmount', 100)}ml</span>
          <input type="hidden" id="set-defformula" value="${getSetting('defaultFormulaAmount', 100)}">
          <button class="step-btn" data-target="set-defformula" data-step="5">+</button>
        </div>
      </div>
      <div class="setting-row">
        <label>기본 모유량</label>
        <div class="stepper">
          <button class="step-btn" data-target="set-defbreast" data-step="-5">−</button>
          <span id="set-defbreast-display">${getSetting('defaultBreastfeedAmount', 20)}ml</span>
          <input type="hidden" id="set-defbreast" value="${getSetting('defaultBreastfeedAmount', 20)}">
          <button class="step-btn" data-target="set-defbreast" data-step="5">+</button>
        </div>
      </div>
      <div class="setting-row">
        <label>분유 제품</label>
        <select id="set-product" style="flex:1;">
          ${(getSetting('formulaProducts', ['트루맘 클래식'])).map(p => `<option value="${p}" ${p===getSetting('defaultFormulaProduct','트루맘 클래식')?'selected':''}>${p}</option>`).join('')}
        </select>
      </div>
    </div>

    <div class="setting-group">
      <h3>앱 정보</h3>
      <div class="setting-row"><label>버전</label><span>2.0.0-sync</span></div>
      <div class="setting-row"><label>상위 프로젝트</label><span>기찬다이어리 (WPF)</span></div>
      <div class="setting-row"><label>데이터 소스</label><span>${user ? 'Firebase 실시간' : 'IndexedDB (로컬)'}</span></div>
    </div>`;

  bindEvents(container);
}

function buildAuthSection(user) {
  if (user) {
    return `
      <div class="setting-row">
        <label>계정</label>
        <span>${user.email}</span>
      </div>
      <div class="setting-row">
        <label>상태</label>
        <span style="color:var(--cat-feed);font-weight:600;">● 동기화 중</span>
      </div>
      <button class="import-btn" id="signout-btn" style="background:var(--cat-health);margin-top:8px;">로그아웃</button>`;
  } else {
    return `
      <div class="import-info" style="margin-bottom:8px;">Google 계정으로 로그인하면 실시간 동기화가 시작됩니다.</div>
      <button class="import-btn" id="signin-btn">🔗 Google 로그인</button>`;
  }
}

function bindEvents(container) {
  const fileInput = container.querySelector('#file-input');
  const statusEl = container.querySelector('#import-status');

  container.querySelector('#import-btn')?.addEventListener('click', () => fileInput.click());

  fileInput.addEventListener('change', async (e) => {
    const file = e.target.files[0];
    if (!file) return;
    statusEl.textContent = '파일 분석 중...';
    try {
      const events = await parseExcelFile(file);
      await saveEvents(events);
      statusEl.textContent = `✓ ${events.length}건 가져오기 완료!`;
      container.querySelector('#import-info').textContent =
        `마지막 가져오기: ${new Date().toLocaleString('ko-KR')} (${events.length}건)`;
      if (onImportCallback) onImportCallback(events);
    } catch (err) {
      statusEl.textContent = `✗ 오류: ${err.message}`;
      statusEl.style.color = 'var(--cat-health)';
    }
    fileInput.value = '';
  });

  // Firebase auth buttons
  container.querySelector('#signin-btn')?.addEventListener('click', async () => {
    try {
      await signIn();
    } catch (err) {
      alert('로그인 실패: ' + err.message);
    }
  });

  container.querySelector('#signout-btn')?.addEventListener('click', async () => {
    await signOut();
  });

  container.querySelector('#set-pagesize')?.addEventListener('change', (e) => {
    setSetting('pageSize', parseInt(e.target.value));
    syncSetting('pageSize', parseInt(e.target.value));
  });

  container.querySelector('#set-babyname')?.addEventListener('change', (e) => {
    const v = e.target.value.trim();
    setSetting('babyName', v);
    syncSetting('babyName', v);
  });

  // Stepper buttons
  container.querySelectorAll('.step-btn').forEach(btn => {
    btn.addEventListener('click', () => {
      const targetId = btn.dataset.target;
      const step = parseFloat(btn.dataset.step);
      const input = container.querySelector(`#${targetId}`);
      if (!input) return;
      let val = parseFloat(input.value) + step;
      val = Math.max(0, val);
      input.value = val;

      const displayEl = container.querySelector(`#${targetId}-display`);
      const settingMap = {
        'set-interval': { key: 'fixedFeedingInterval', transform: v => Math.round(v * 3600), display: v => `${v}시간` },
        'set-avgcount': { key: 'averageFeedingCount', transform: v => Math.round(v), display: v => `${Math.round(v)}회` },
        'set-defformula': { key: 'defaultFormulaAmount', transform: v => Math.round(v), display: v => `${Math.round(v)}ml` },
        'set-defbreast': { key: 'defaultBreastfeedAmount', transform: v => Math.round(v), display: v => `${Math.round(v)}ml` },
      };
      const cfg = settingMap[targetId];
      if (cfg) {
        if (displayEl) displayEl.textContent = cfg.display(val);
        setSetting(cfg.key, cfg.transform(val));
        syncSetting(cfg.key, cfg.transform(val));
      }
    });
  });

  // Formula product
  container.querySelector('#set-product')?.addEventListener('change', (e) => {
    const v = e.target.value.trim();
    setSetting('defaultFormulaProduct', v);
    syncSetting('defaultFormulaProduct', v);
  });
}

async function syncSetting(key, value) {
  const user = getCurrentUser();
  if (!user) return;
  try {
    await saveSettingsToFirestore({ [key]: value });
  } catch (err) {
    console.warn('Setting sync failed:', err);
  }
}
