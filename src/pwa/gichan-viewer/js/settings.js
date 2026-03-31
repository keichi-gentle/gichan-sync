import { parseExcelFile } from './excel-parser.js';
import { saveEvents, getSetting, setSetting } from './storage.js';

let onImportCallback = null;

export function renderSettings(container, onImport) {
  onImportCallback = onImport;

  const lastDate = getSetting('lastImportDate');
  const lastCount = getSetting('lastImportCount', 0);
  const pageSize = getSetting('pageSize', 30);
  const babyName = getSetting('babyName', '');
  const importInfo = lastDate
    ? `마지막 가져오기: ${new Date(lastDate).toLocaleString('ko-KR')} (${lastCount}건)`
    : '아직 가져온 파일이 없습니다.';

  container.innerHTML = `
    <div class="setting-group">
      <h3>데이터 관리</h3>
      <button class="import-btn" id="import-btn">📂 Excel 파일 가져오기</button>
      <input type="file" id="file-input" accept=".xlsx,.xls" style="display:none">
      <div class="import-info" id="import-info">${importInfo}</div>
      <div id="import-status" style="text-align:center;margin-top:8px;color:var(--cat-feed);font-weight:600;"></div>
    </div>

    <div class="setting-group">
      <h3>표시 설정</h3>
      <div class="setting-row">
        <label>페이지당 표시 건수</label>
        <select id="set-pagesize">
          ${[10,20,30,50].map(n => `<option value="${n}" ${n===pageSize?'selected':''}>${n}건</option>`).join('')}
        </select>
      </div>
      <div class="setting-row">
        <label>아기 이름</label>
        <input type="text" id="set-babyname" value="${babyName}" placeholder="이름 입력" style="width:120px;text-align:right;">
      </div>
    </div>

    <div class="setting-group">
      <h3>앱 정보</h3>
      <div class="setting-row"><label>버전</label><span>1.0.0</span></div>
      <div class="setting-row"><label>상위 프로젝트</label><span>기찬다이어리 (WPF)</span></div>
    </div>`;

  bindEvents(container);
}

function bindEvents(container) {
  const fileInput = container.querySelector('#file-input');
  const statusEl = container.querySelector('#import-status');

  container.querySelector('#import-btn').addEventListener('click', () => fileInput.click());

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

  container.querySelector('#set-pagesize').addEventListener('change', (e) => {
    setSetting('pageSize', parseInt(e.target.value));
  });

  container.querySelector('#set-babyname').addEventListener('change', (e) => {
    setSetting('babyName', e.target.value.trim());
  });
}
