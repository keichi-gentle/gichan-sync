import * as C from './calc.js';
import { getSetting, setSetting } from './storage.js';

let timerInterval = null;
let cachedEvents = [];
let syncOnline = false;
let lastSyncTime = null;
let built = false; // DOM 구조 생성 여부
let expanded = false;

export function setSyncStatus(online, time = null) {
  syncOnline = online;
  if (time) lastSyncTime = time;
}

export function initScoreboard(events, container) {
  cachedEvents = events;
  expanded = getSetting('scoreboardExpanded', false);
  built = false;
  if (timerInterval) clearInterval(timerInterval);
  render(container);
  timerInterval = setInterval(() => render(container), 1000);

  container.addEventListener('click', () => {
    expanded = !expanded;
    setSetting('scoreboardExpanded', expanded);
    const sb = container.querySelector('.scoreboard');
    if (sb) sb.className = expanded ? 'scoreboard sb-expanded' : 'scoreboard';
  });
}

export function updateScoreboardEvents(events) {
  cachedEvents = events;
}

export function stopScoreboard() {
  if (timerInterval) { clearInterval(timerInterval); timerInterval = null; }
}

// 최초 1회 HTML 구조 생성
function buildHTML(container) {
  container.innerHTML = `
    <div class="scoreboard${expanded ? ' sb-expanded' : ''}">
      <div class="sb-clock">
        <div class="sb-time" id="sb-time"></div>
        <div class="sb-date" id="sb-date"></div>
      </div>

      <div class="sb-sections">
        <div class="sb-section">
          <div class="sb-label">최근 수유</div>
          <div class="sb-value cat-feed" id="sb-feed-time"></div>
          <div class="sb-sub" id="sb-feed-elapsed"></div>
        </div>

        <div class="sb-divider"></div>

        <div class="sb-section">
          <div class="sb-label">다음 수유</div>
          <div class="sb-value cat-feed" id="sb-next-time"></div>
          <div class="sb-sub" id="sb-next-remain"></div>
          <div class="sb-sub2" id="sb-next-remain2" style="display:none"></div>
          <div class="sb-progress"><div class="sb-progress-fill" id="sb-progress"></div></div>
        </div>

        <div class="sb-divider"></div>

        <div class="sb-section">
          <div class="sb-label">오늘 수유</div>
          <div class="sb-value cat-feed" id="sb-today-feed"></div>
          <div class="sb-sub cat-feed" id="sb-today-count" style="display:none"></div>
          <div class="sb-sub2 cat-feed" id="sb-avg-interval"></div>
        </div>
      </div>

      <div class="sb-bowel">
        <div class="sb-bowel-half">
          <div class="sb-bowel-col">
            <div class="sb-label" style="color:var(--cat-urine)">소변</div>
            <div class="sb-value" id="sb-urine-time"></div>
          </div>
          <div class="sb-bowel-col">
            <div class="sb-sub" style="color:var(--cat-urine)" id="sb-urine-elapsed"></div>
            <div class="sb-sub" id="sb-urine-count"></div>
          </div>
        </div>
        <div class="sb-bowel-divider"></div>
        <div class="sb-bowel-half">
          <div class="sb-bowel-col">
            <div class="sb-label" style="color:var(--cat-stool)">대변</div>
            <div class="sb-value" id="sb-stool-time"></div>
          </div>
          <div class="sb-bowel-col">
            <div class="sb-sub" style="color:var(--cat-stool)" id="sb-stool-elapsed"></div>
            <div class="sb-sub" id="sb-stool-count"></div>
          </div>
        </div>
      </div>
    </div>`;
  built = true;
}

// 매초 텍스트만 갱신 (DOM 유지 → CSS 애니메이션 지속)
function render(container) {
  if (!built) buildHTML(container);

  const events = cachedEvents;
  const now = new Date();

  // Time / Date / Day
  const timeStr = now.toTimeString().slice(0, 8).replace(/:/g, ' : ');
  const dateStr = `${now.getFullYear()}/${String(now.getMonth()+1).padStart(2,'0')}/${String(now.getDate()).padStart(2,'0')}`;
  const dayNames = ['일','월','화','수','목','금','토'];
  const dayOfWeek = dayNames[now.getDay()];

  let dayNumberStr = '';
  const birthDateStr = getSetting('babyBirthDate');
  if (birthDateStr) {
    const birth = new Date(birthDateStr);
    if (!isNaN(birth)) {
      const dayNum = Math.floor((now - birth) / 86400000) + 1;
      dayNumberStr = ` · ${dayNum}일차`;
    }
  }

  document.getElementById('sb-time').textContent = timeStr;

  const dateEl = document.getElementById('sb-date');
  const syncLampClass = syncOnline ? 'online' : 'offline';
  const syncText = syncOnline ? 'On-Line' : 'Off-Line';
  dateEl.innerHTML = `${dateStr} (${dayOfWeek})${dayNumberStr} <span class="sb-sync-lamp ${syncLampClass}"></span><span class="sb-sync-status ${syncLampClass}">${syncText}</span>${lastSyncTime ? ` <span class="sb-sync-time">${lastSyncTime}</span>` : ''}`;

  // Feed
  const lastFeed = events.filter(e => C.isFeeding(e) && C.getFullDateTime(e))
    .sort((a, b) => C.getFullDateTime(b) - C.getFullDateTime(a))[0];

  let feedTime = '-', feedElapsed = '-', nextFeedStr = '-', nextRemain = '', progressPct = 0, isUrgent = false;
  if (lastFeed) {
    const ft = C.getFullDateTime(lastFeed);
    feedTime = ft.toTimeString().slice(0, 5);
    const elapsedMs = now - ft;
    feedElapsed = formatShortElapsed(elapsedMs);

    const intervalMs = (getSetting('fixedFeedingInterval', 10800)) * 1000;
    const nextDt = new Date(ft.getTime() + intervalMs);
    nextFeedStr = nextDt.toTimeString().slice(0, 5);
    const remainMs = nextDt - now;
    progressPct = Math.min(100, Math.max(0, (elapsedMs / intervalMs) * 100));

    if (remainMs > 0) {
      const rh = Math.floor(remainMs / 3600000);
      const rm = Math.floor((remainMs % 3600000) / 60000);
      nextRemain = `${rh}시간 ${rm}분 남음`;
      isUrgent = remainMs <= 1800000;
    } else {
      const overMs = now - nextDt;
      const oh = Math.floor(overMs / 3600000);
      const om = Math.floor((overMs % 3600000) / 60000);
      nextRemain = `초과 ${oh}시간${om}분`;
      isUrgent = true;
      progressPct = 100;
    }
  }

  const todayTotal = C.getDailyFeedTotal(events, now);
  const todayCount = C.getDailyFeedCount(events, now);
  const avgInterval = C.formatInterval(C.getAvgFeedingInterval(events, 10));

  document.getElementById('sb-feed-time').textContent = feedTime;

  const feedElapsedEl = document.getElementById('sb-feed-elapsed');
  feedElapsedEl.textContent = feedElapsed;
  feedElapsedEl.className = isUrgent ? 'sb-sub' : 'sb-sub cat-feed';
  feedElapsedEl.style.color = isUrgent ? 'var(--cat-health)' : '';

  document.getElementById('sb-next-time').textContent = nextFeedStr;

  // 다음 수유: DOM 유지하며 텍스트만 갱신 (blink 애니메이션 보존)
  const nextRemainEl = document.getElementById('sb-next-remain');
  const nextRemain2El = document.getElementById('sb-next-remain2');
  if (expanded) {
    const remainTime = nextRemain.replace(' 남음', '').replace('남음', '');
    const isRemain = nextRemain.includes('남음');
    nextRemainEl.textContent = remainTime;
    nextRemain2El.textContent = isRemain ? '남음' : '';
    nextRemain2El.style.display = isRemain ? '' : 'none';
  } else {
    nextRemainEl.textContent = nextRemain;
    nextRemain2El.style.display = 'none';
  }
  nextRemainEl.className = isUrgent ? 'sb-sub sb-urgent' : 'sb-sub';
  nextRemain2El.className = isUrgent ? 'sb-sub2 sb-urgent' : 'sb-sub2';

  document.getElementById('sb-progress').style.width = progressPct + '%';

  // 오늘 수유: DOM 유지
  const todayFeedEl = document.getElementById('sb-today-feed');
  const todayCountEl = document.getElementById('sb-today-count');
  if (expanded) {
    todayFeedEl.textContent = `${todayTotal}ml`;
    todayCountEl.textContent = `/ ${todayCount}회`;
    todayCountEl.style.display = '';
  } else {
    todayFeedEl.textContent = `${todayTotal}ml / ${todayCount}회`;
    todayCountEl.style.display = 'none';
  }

  document.getElementById('sb-avg-interval').textContent = `평균텀 ${avgInterval}`;

  // Bowel
  const summary = C.getDailySummary(events, now);
  const urineMs = C.getLastUrineElapsed(events);
  const stoolMs = C.getLastStoolElapsed(events);

  const lastUrine = events.filter(e => e.category === '배변' && e.hasUrine && C.getFullDateTime(e))
    .sort((a, b) => C.getFullDateTime(b) - C.getFullDateTime(a))[0];
  const lastStool = events.filter(e => e.category === '배변' && e.hasStool && C.getFullDateTime(e))
    .sort((a, b) => C.getFullDateTime(b) - C.getFullDateTime(a))[0];

  document.getElementById('sb-urine-time').textContent = lastUrine ? C.getFullDateTime(lastUrine).toTimeString().slice(0,5) : '-';
  document.getElementById('sb-urine-elapsed').textContent = formatShortElapsed(urineMs);
  document.getElementById('sb-urine-count').textContent = `오늘 ${summary.urineCount}회`;

  document.getElementById('sb-stool-time').textContent = lastStool ? C.getFullDateTime(lastStool).toTimeString().slice(0,5) : '-';
  document.getElementById('sb-stool-elapsed').textContent = formatShortElapsed(stoolMs);
  document.getElementById('sb-stool-count').textContent = `오늘 ${summary.stoolCount}회`;
}

function formatShortElapsed(ms) {
  if (ms == null) return '-';
  const totalMin = Math.floor(ms / 60000);
  const h = Math.floor(totalMin / 60);
  const m = totalMin % 60;
  return `${h}시간 ${m}분`;
}
