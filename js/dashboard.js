import * as C from './calc.js';

export function renderDashboard(events, container) {
  const now = new Date();
  container.innerHTML = '';

  if (!events.length) {
    container.innerHTML = '<div class="empty-state">데이터가 없습니다.<br>설정 탭에서 Excel 파일을 가져와 주세요.</div>';
    return;
  }

  container.innerHTML = buildFeedCard(events, now)
    + buildBowelCard(events, now)
    + buildHygieneCard(events, now)
    + buildBodyCard(events)
    + buildHealthCard(events)
    + buildEtcCard(events);
}

function buildFeedCard(events, now) {
  const lastFeed = events.filter(e => C.isFeeding(e) && C.getFullDateTime(e))
    .sort((a, b) => C.getFullDateTime(b) - C.getFullDateTime(a))[0];

  let time = '-', detail = '-', elapsed = '-';
  if (lastFeed) {
    const ft = C.getFullDateTime(lastFeed);
    time = ft.toTimeString().slice(0, 5);
    detail = `${lastFeed.detail} ${lastFeed.amount}`;
    if (lastFeed.feedingCount > 1) detail += `, 분할 ${lastFeed.feedingCount}회`;
    elapsed = C.formatElapsed(now - ft);
  }

  const todayTotal = C.getDailyFeedTotal(events, now);
  const todayCount = C.getDailyFeedCount(events, now);
  const avgInterval = C.formatInterval(C.getAvgFeedingInterval(events, 10));

  return card('수유', 'cat-feed', [
    row('최근 수유', time),
    row('경과', elapsed),
    row('내용', detail),
    row('오늘 누적', `${todayTotal}ml / ${todayCount}회`),
    row('평균 수유텀', avgInterval),
  ]);
}

function buildBowelCard(events, now) {
  const summary = C.getDailySummary(events, now);
  const urineEl = C.formatElapsed(C.getLastUrineElapsed(events));
  const stoolEl = C.formatElapsed(C.getLastStoolElapsed(events));

  return card('배변', 'cat-bowel', [
    row('소변 경과', urineEl),
    row('대변 경과', stoolEl),
    row('오늘 소변', `${summary.urineCount}회`),
    row('오늘 대변', `${summary.stoolCount}회`),
  ]);
}

function buildHygieneCard(events, now) {
  const hEvents = events.filter(e => e.category === '위생관리' && C.getFullDateTime(e))
    .sort((a, b) => C.getFullDateTime(b) - C.getFullDateTime(a));

  const find = (keywords) => hEvents.find(e => keywords.some(k => e.detail.includes(k)));
  const fmt = (evt) => {
    if (!evt) return { time: '-', elapsed: '' };
    const dt = C.getFullDateTime(evt);
    const t = `${String(dt.getMonth()+1).padStart(2,'0')}/${String(dt.getDate()).padStart(2,'0')} ${dt.toTimeString().slice(0,5)}`;
    return { time: t, elapsed: C.formatElapsed(now - dt) };
  };

  const wash = fmt(find(['세안', '샤워']));
  const bath = fmt(find(['목욕', '샤워']));
  const nails = fmt(find(['손발톱']));

  return card('위생 관리', 'cat-hygiene', [
    rowWithElapsed('세안', wash.time, wash.elapsed),
    rowWithElapsed('목욕', bath.time, bath.elapsed),
    rowWithElapsed('손발톱', nails.time, nails.elapsed),
  ]);
}

function buildBodyCard(events) {
  const body = events.filter(e => e.category === '신체측정' && C.getFullDateTime(e))
    .sort((a, b) => C.getFullDateTime(b) - C.getFullDateTime(a));
  const find = (kw) => body.find(e => e.detail.includes(kw));
  const fmt = (evt) => evt ? `${fmtDate(C.getFullDateTime(evt))} ${evt.amount}` : '-';

  return card('신체 측정', 'cat-body', [
    row('키', fmt(find('키'))),
    row('몸무게', fmt(find('몸무게'))),
    row('머리둘레', fmt(find('머리'))),
  ]);
}

function buildHealthCard(events) {
  const last = events.filter(e => e.category === '건강관리' && C.getFullDateTime(e))
    .sort((a, b) => C.getFullDateTime(b) - C.getFullDateTime(a))[0];
  const info = last ? `${fmtDate(C.getFullDateTime(last))} ${last.detail} ${last.note}` : '-';
  return card('건강 관리', 'cat-health', [row('최근', info)]);
}

function buildEtcCard(events) {
  const last = events.filter(e => e.category === '기타' && C.getFullDateTime(e))
    .sort((a, b) => C.getFullDateTime(b) - C.getFullDateTime(a))[0];
  const info = last ? `${fmtDate(C.getFullDateTime(last))} ${last.detail} ${last.note}` : '-';
  return card('기타', 'cat-etc', [row('최근', info)]);
}

// HTML helpers
function card(title, colorClass, rows) {
  return `<div class="card"><div class="card-title ${colorClass}">${title}</div>${rows.join('')}</div>`;
}

function row(label, value) {
  return `<div class="card-row"><span class="label">${label}</span><span class="value">${value}</span></div>`;
}

function rowWithElapsed(label, value, elapsed) {
  return `<div class="card-row"><span class="label">${label}</span><span class="value">${value}<span class="elapsed">${elapsed}</span></span></div>`;
}

function fmtDate(d) {
  if (!d) return '';
  return `${String(d.getMonth()+1).padStart(2,'0')}/${String(d.getDate()).padStart(2,'0')}`;
}
