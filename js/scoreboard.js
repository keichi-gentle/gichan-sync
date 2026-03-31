import * as C from './calc.js';

let timerInterval = null;
let cachedEvents = [];

export function initScoreboard(events, container) {
  cachedEvents = events;
  if (timerInterval) clearInterval(timerInterval);
  render(container);
  timerInterval = setInterval(() => render(container), 1000);
}

export function updateScoreboardEvents(events) {
  cachedEvents = events;
}

export function stopScoreboard() {
  if (timerInterval) { clearInterval(timerInterval); timerInterval = null; }
}

function render(container) {
  const events = cachedEvents;
  const now = new Date();

  // Time / Date / Day
  const timeStr = now.toTimeString().slice(0, 8).replace(/:/g, ' : ');
  const dateStr = `${now.getFullYear()}/${String(now.getMonth()+1).padStart(2,'0')}/${String(now.getDate()).padStart(2,'0')}`;
  const dayNames = ['일','월','화','수','목','금','토'];
  const dayOfWeek = dayNames[now.getDay()];

  // Feed
  const lastFeed = events.filter(e => C.isFeeding(e) && C.getFullDateTime(e))
    .sort((a, b) => C.getFullDateTime(b) - C.getFullDateTime(a))[0];

  let feedTime = '-', feedElapsed = '-', nextFeedStr = '-', nextRemain = '', progressPct = 0, isUrgent = false;
  if (lastFeed) {
    const ft = C.getFullDateTime(lastFeed);
    feedTime = ft.toTimeString().slice(0, 5);
    const elapsedMs = now - ft;
    feedElapsed = formatShortElapsed(elapsedMs);

    // Next feed (3시간 고정텀 기본)
    const intervalMs = 3 * 3600000;
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

  // Today feed
  const todayTotal = C.getDailyFeedTotal(events, now);
  const todayCount = C.getDailyFeedCount(events, now);
  const avgInterval = C.formatInterval(C.getAvgFeedingInterval(events, 10));

  // Bowel
  const summary = C.getDailySummary(events, now);
  const urineMs = C.getLastUrineElapsed(events);
  const stoolMs = C.getLastStoolElapsed(events);

  const lastUrine = events.filter(e => e.category === '배변' && e.hasUrine && C.getFullDateTime(e))
    .sort((a, b) => C.getFullDateTime(b) - C.getFullDateTime(a))[0];
  const lastStool = events.filter(e => e.category === '배변' && e.hasStool && C.getFullDateTime(e))
    .sort((a, b) => C.getFullDateTime(b) - C.getFullDateTime(a))[0];

  const urineTime = lastUrine ? C.getFullDateTime(lastUrine).toTimeString().slice(0,5) : '-';
  const stoolTime = lastStool ? C.getFullDateTime(lastStool).toTimeString().slice(0,5) : '-';

  const urgentClass = isUrgent ? ' sb-urgent' : '';

  container.innerHTML = `
    <div class="scoreboard">
      <div class="sb-clock">
        <div class="sb-time">${timeStr}</div>
        <div class="sb-date">${dateStr} (${dayOfWeek})</div>
      </div>

      <div class="sb-sections">
        <div class="sb-section">
          <div class="sb-label">최근 수유</div>
          <div class="sb-value cat-feed">${feedTime}</div>
          <div class="sb-sub cat-feed">${feedElapsed}</div>
        </div>

        <div class="sb-divider"></div>

        <div class="sb-section">
          <div class="sb-label">다음 수유</div>
          <div class="sb-value cat-feed">${nextFeedStr}</div>
          <div class="sb-sub${urgentClass}">${nextRemain}</div>
          <div class="sb-progress"><div class="sb-progress-fill" style="width:${progressPct}%"></div></div>
        </div>

        <div class="sb-divider"></div>

        <div class="sb-section">
          <div class="sb-label">오늘 수유</div>
          <div class="sb-value cat-feed">${todayTotal}ml / ${todayCount}회</div>
          <div class="sb-sub cat-feed">평균텀 ${avgInterval}</div>
        </div>
      </div>

      <div class="sb-bowel">
        <div class="sb-bowel-half">
          <div class="sb-bowel-col">
            <div class="sb-label" style="color:var(--cat-urine)">소변</div>
            <div class="sb-value">${urineTime}</div>
          </div>
          <div class="sb-bowel-col">
            <div class="sb-sub" style="color:var(--cat-urine)">${formatShortElapsed(urineMs)}</div>
            <div class="sb-sub">오늘 ${summary.urineCount}회</div>
          </div>
        </div>
        <div class="sb-bowel-divider"></div>
        <div class="sb-bowel-half">
          <div class="sb-bowel-col">
            <div class="sb-label" style="color:var(--cat-stool)">대변</div>
            <div class="sb-value">${stoolTime}</div>
          </div>
          <div class="sb-bowel-col">
            <div class="sb-sub" style="color:var(--cat-stool)">${formatShortElapsed(stoolMs)}</div>
            <div class="sb-sub">오늘 ${summary.stoolCount}회</div>
          </div>
        </div>
      </div>
    </div>`;
}

function formatShortElapsed(ms) {
  if (ms == null) return '-';
  const totalMin = Math.floor(ms / 60000);
  const h = Math.floor(totalMin / 60);
  const m = totalMin % 60;
  return `${h}시간 ${m}분`;
}
