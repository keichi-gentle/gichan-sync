import * as C from './calc.js';
import { getSetting } from './storage.js';

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

  let dayNumberStr = '';
  const birthDateStr = getSetting('babyBirthDate');
  if (birthDateStr) {
    const birth = new Date(birthDateStr);
    if (!isNaN(birth)) {
      const dayNum = Math.floor((now - birth) / 86400000) + 1;
      dayNumberStr = ` · ${dayNum}일차`;
    }
  }

  // Feed
  const lastFeed = events.filter(e => C.isFeeding(e) && C.getFullDateTime(e))
    .sort((a, b) => C.getFullDateTime(b) - C.getFullDateTime(a))[0];

  let feedTime = '-', feedDetail = '', feedElapsed = '-', nextFeedStr = '-', nextRemain = '', progressPct = 0, isUrgent = false;
  if (lastFeed) {
    const ft = C.getFullDateTime(lastFeed);
    feedTime = ft.toTimeString().slice(0, 5);
    feedDetail = lastFeed.detail ? ` ${lastFeed.detail}` : '';
    if (lastFeed.amount && lastFeed.amount !== '-') feedDetail += ` ${lastFeed.amount}`;
    const elapsedMs = now - ft;
    feedElapsed = formatShortElapsed(elapsedMs);

    const intervalMs = (getSetting('fixedFeedingInterval') || 10800) * 1000;
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
      nextRemain = `시간 초과! ${oh}시간${om}분`;
      isUrgent = true;
      progressPct = 100;
    }
  }

  // Today feed
  const todayTotal = C.getDailyFeedTotal(events, now);
  const todayCount = C.getDailyFeedCount(events, now);

  // 일일 평균 텀
  const todayFeeds = events
    .filter(e => C.isFeeding(e) && C.getFullDateTime(e) && C.getFullDateTime(e).toDateString() === now.toDateString())
    .sort((a, b) => C.getFullDateTime(a) - C.getFullDateTime(b));
  let dailyAvg = '-';
  if (todayFeeds.length >= 2) {
    const first = C.getFullDateTime(todayFeeds[0]);
    const last = C.getFullDateTime(todayFeeds[todayFeeds.length - 1]);
    const avgMin = (last - first) / 60000 / (todayFeeds.length - 1);
    const ah = Math.floor(avgMin / 60), am = Math.floor(avgMin % 60);
    dailyAvg = `${ah}:${String(am).padStart(2,'0')}`;
  }

  // 24H 평균 텀
  const h24Avg = C.formatInterval(C.getAvgFeedingInterval(events, getSetting('averageFeedingCount') || 10));

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
        <div class="sb-date">${dateStr} (${dayOfWeek})${dayNumberStr}</div>
      </div>

      <div class="sb-grid">
        <!-- 수유 정보 -->
        <div class="sb-group">
          <div class="sb-group-title cat-feed">[ 수유 정보 ]</div>
          <div class="sb-row">
            <div class="sb-cell">
              <div class="sb-label">최근 수유</div>
              <div class="sb-value cat-feed">${feedTime}${feedDetail}</div>
              <div class="sb-sub cat-feed">${feedElapsed}</div>
            </div>
            <div class="sb-cell">
              <div class="sb-label">오늘 수유</div>
              <div class="sb-value cat-feed">${todayTotal}ml / ${todayCount}회</div>
              <div class="sb-sub cat-feed">일일 평균 텀 ${dailyAvg}</div>
              <div class="sb-sub cat-feed">24H 평균 텀 ${h24Avg}</div>
            </div>
          </div>
          <div class="sb-row">
            <div class="sb-cell" style="flex:1;">
              <div class="sb-label">다음 수유</div>
              <div class="sb-value cat-feed">${nextFeedStr}</div>
              <div class="sb-sub${urgentClass}">${nextRemain}</div>
              <div class="sb-progress"><div class="sb-progress-fill" style="width:${progressPct}%"></div></div>
            </div>
          </div>
        </div>

        <!-- 배변 정보 -->
        <div class="sb-group">
          <div class="sb-group-title cat-bowel">[ 배변 정보 ]</div>
          <div class="sb-row">
            <div class="sb-cell">
              <div class="sb-label" style="color:var(--cat-urine)">소변</div>
              <div class="sb-value">${urineTime}</div>
              <div class="sb-sub" style="color:var(--cat-urine)">${formatShortElapsed(urineMs)}</div>
              <div class="sb-sub">${summary.urineCount}회</div>
            </div>
          </div>
          <div class="sb-row">
            <div class="sb-cell">
              <div class="sb-label" style="color:var(--cat-stool)">대변</div>
              <div class="sb-value">${stoolTime}</div>
              <div class="sb-sub" style="color:var(--cat-stool)">${formatShortElapsed(stoolMs)}</div>
              <div class="sb-sub">${summary.stoolCount}회</div>
            </div>
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
  return `경과: ${h}시간 ${m}분`;
}
