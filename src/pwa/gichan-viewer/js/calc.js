// CalculationService — ported from WPF C# CalculationService.cs

export function getFullDateTime(evt) {
  if (!evt.date) return null;
  if (!evt.time) return evt.date;
  const [h, m] = evt.time.split(':').map(Number);
  const dt = new Date(evt.date);
  dt.setHours(h || 0, m || 0, 0, 0);
  return dt;
}

export function isFeeding(evt) {
  return evt.category === '수유';
}

export function getDailyFeedTotal(events, date) {
  const d = toDateStr(date);
  return events.filter(e => isFeeding(e) && toDateStr(e.date) === d)
    .reduce((sum, e) => sum + getTotalFeedAmount(e), 0);
}

export function getDailyFeedCount(events, date) {
  const d = toDateStr(date);
  return events.filter(e => isFeeding(e) && toDateStr(e.date) === d).length;
}

export function getTotalFeedAmount(evt) {
  return (evt.formulaAmount || 0) + (evt.breastfeedAmount || 0);
}

export function getDailySummary(events, date) {
  const d = toDateStr(date);
  const dayEvents = events.filter(e => toDateStr(e.date) === d);
  const bowels = dayEvents.filter(e => e.category === '배변');
  return {
    urineCount: bowels.filter(e => e.hasUrine).length,
    stoolCount: bowels.filter(e => e.hasStool).length,
  };
}

export function getLastElapsed(events, filterFn) {
  const match = events.filter(e => filterFn(e) && getFullDateTime(e))
    .sort((a, b) => getFullDateTime(b) - getFullDateTime(a))[0];
  if (!match) return null;
  return Date.now() - getFullDateTime(match).getTime();
}

export function getLastUrineElapsed(events) {
  return getLastElapsed(events, e => e.category === '배변' && e.hasUrine);
}

export function getLastStoolElapsed(events) {
  return getLastElapsed(events, e => e.category === '배변' && e.hasStool);
}

export function getAvgFeedingInterval(events, recentCount = 10) {
  const feedings = events.filter(e => isFeeding(e) && getFullDateTime(e))
    .sort((a, b) => getFullDateTime(a) - getFullDateTime(b));
  if (feedings.length < 2) return null;
  const recent = feedings.slice(-(recentCount + 1));
  const intervals = [];
  for (let i = 1; i < recent.length; i++) {
    intervals.push(getFullDateTime(recent[i]) - getFullDateTime(recent[i - 1]));
  }
  if (intervals.length === 0) return null;
  return intervals.reduce((a, b) => a + b, 0) / intervals.length;
}

export function formatElapsed(ms) {
  if (ms == null) return '-';
  const totalMin = Math.floor(ms / 60000);
  const hours = Math.floor(totalMin / 60);
  const mins = totalMin % 60;
  if (hours >= 24) {
    const days = Math.floor(hours / 24);
    const h = hours % 24;
    return `${days}일 ${h}시간 경과`;
  }
  return `${hours}시간 ${mins}분 경과`;
}

export function formatInterval(ms) {
  if (ms == null) return '-';
  const totalMin = Math.floor(ms / 60000);
  const h = Math.floor(totalMin / 60);
  const m = totalMin % 60;
  return `${h}:${String(m).padStart(2, '0')}`;
}

export function getCategoryClass(evt) {
  if (evt.category === '배변') {
    if (evt.hasUrine && !evt.hasStool) return 'cat-urine';
    if (evt.hasStool && !evt.hasUrine) return 'cat-stool';
    return 'cat-bowel';
  }
  const map = { '수유': 'cat-feed', '위생관리': 'cat-hygiene', '신체측정': 'cat-body', '건강관리': 'cat-health', '기타': 'cat-etc' };
  return map[evt.category] || 'cat-etc';
}

export function getCategoryColor(category) {
  const s = getComputedStyle(document.documentElement);
  const map = { '수유': '--cat-feed', '배변': '--cat-bowel', '위생관리': '--cat-hygiene', '신체측정': '--cat-body', '건강관리': '--cat-health', '기타': '--cat-etc' };
  return s.getPropertyValue(map[category] || '--cat-etc').trim();
}

function toDateStr(d) {
  if (!d) return '';
  if (d instanceof Date) return d.toISOString().slice(0, 10);
  return String(d).slice(0, 10);
}
