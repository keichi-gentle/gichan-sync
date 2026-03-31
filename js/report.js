import * as C from './calc.js';

let charts = [];
let allEvents = [];

export function renderReport(events, container) {
  allEvents = events;
  destroyCharts();

  if (!events.length) {
    container.innerHTML = '<div class="empty-state">데이터가 없습니다.<br>설정 탭에서 Excel 파일을 가져와 주세요.</div>';
    return;
  }

  container.innerHTML = buildPeriodButtons() + '<div id="rpt-summary"></div><div id="rpt-charts"></div>';
  bindPeriodButtons(container);
  refreshReport('7일');
}

function buildPeriodButtons() {
  return `<div class="period-buttons">
    ${['7일','14일','30일','전체'].map(p =>
      `<button class="period-btn${p === '7일' ? ' active' : ''}" data-period="${p}">${p}</button>`
    ).join('')}
  </div>`;
}

function bindPeriodButtons(container) {
  container.querySelector('.period-buttons').addEventListener('click', e => {
    const btn = e.target.closest('.period-btn');
    if (!btn) return;
    container.querySelectorAll('.period-btn').forEach(b => b.classList.remove('active'));
    btn.classList.add('active');
    refreshReport(btn.dataset.period);
  });
}

function refreshReport(period) {
  destroyCharts();
  const filtered = filterByPeriod(allEvents, period);
  renderSummary(filtered, period);
  renderCharts(filtered);
}

function filterByPeriod(events, period) {
  if (period === '전체') return events;
  const days = parseInt(period);
  const cutoff = new Date(); cutoff.setDate(cutoff.getDate() - days + 1); cutoff.setHours(0,0,0,0);
  return events.filter(e => e.date >= cutoff);
}

function renderSummary(events, period) {
  const now = new Date();
  const todayCount = C.getDailyFeedCount(events, now);
  const todayTotal = C.getDailyFeedTotal(events, now);
  const summary = C.getDailySummary(events, now);
  const avgInterval = C.formatInterval(C.getAvgFeedingInterval(events, 10));

  // Daily average feed amount
  const dailyTotals = {};
  events.filter(e => C.isFeeding(e)).forEach(e => {
    const d = e.date.toISOString().slice(0,10);
    dailyTotals[d] = (dailyTotals[d] || 0) + C.getTotalFeedAmount(e);
  });
  const vals = Object.values(dailyTotals);
  const avgDaily = vals.length > 0 ? Math.round(vals.reduce((a,b)=>a+b,0) / vals.length) : 0;

  document.getElementById('rpt-summary').innerHTML = `
    <div class="summary-wrap">
      <div class="summary-card"><div class="label">오늘 수유 횟수</div><div class="value cat-feed">${todayCount}회</div></div>
      <div class="summary-card"><div class="label">오늘 수유량</div><div class="value cat-feed">${todayTotal}ml</div></div>
      <div class="summary-card"><div class="label">오늘 배변</div><div class="value cat-bowel">소${summary.urineCount}/대${summary.stoolCount}</div></div>
      <div class="summary-card"><div class="label">일평균 수유량</div><div class="value cat-feed">${avgDaily}ml</div></div>
      <div class="summary-card"><div class="label">평균 수유텀</div><div class="value cat-feed">${avgInterval}</div></div>
    </div>`;
}

function renderCharts(events) {
  const el = document.getElementById('rpt-charts');
  const s = getComputedStyle(document.documentElement);
  const feedColor = s.getPropertyValue('--cat-feed').trim();
  const bowelColor = s.getPropertyValue('--cat-bowel').trim();
  const urineColor = s.getPropertyValue('--cat-urine').trim();
  const stoolColor = s.getPropertyValue('--cat-stool').trim();
  const gridColor = s.getPropertyValue('--border').trim();
  const textColor = s.getPropertyValue('--text-mid').trim();

  const defaultScales = {
    x: { ticks: { color: textColor, font: { size: 11 } }, grid: { color: gridColor } },
    y: { ticks: { color: textColor, font: { size: 11 } }, grid: { color: gridColor }, beginAtZero: true },
  };

  const feedings = events.filter(e => C.isFeeding(e) && C.getFullDateTime(e)).sort((a,b) => C.getFullDateTime(a)-C.getFullDateTime(b));

  // Chart 1: Feed amount per feeding (Line)
  el.innerHTML = chartCard('1회 수유량 변화 추이', 'chart1')
    + chartCard('일별 수유량 추이', 'chart2')
    + chartCard('수유텀 분포', 'chart3')
    + chartCard('일별 배변 횟수', 'chart4')
    + chartCard('카테고리별 이벤트 비율', 'chart5')
    + chartCard('일별 이벤트 현황', 'chart6');

  // Chart 1
  if (feedings.length > 0) {
    charts.push(new Chart(el.querySelector('#chart1'), {
      type: 'line',
      data: {
        labels: feedings.map(e => fmtShort(C.getFullDateTime(e))),
        datasets: [{ label: '수유량(ml)', data: feedings.map(e => C.getTotalFeedAmount(e)), borderColor: feedColor, backgroundColor: feedColor + '33', tension: 0.3, fill: true, pointRadius: 2 }],
      },
      options: { responsive: true, plugins: { legend: { display: false } }, scales: defaultScales },
    }));
  }

  // Chart 2: Daily total (Line)
  const dailyGroups = groupByDate(events.filter(e => C.isFeeding(e)));
  if (dailyGroups.length > 0) {
    charts.push(new Chart(el.querySelector('#chart2'), {
      type: 'line',
      data: {
        labels: dailyGroups.map(g => g.label),
        datasets: [{ label: '일별 수유량(ml)', data: dailyGroups.map(g => g.events.reduce((s,e) => s + C.getTotalFeedAmount(e), 0)), borderColor: feedColor, backgroundColor: feedColor + '33', tension: 0.3, fill: true, pointRadius: 3 }],
      },
      options: { responsive: true, plugins: { legend: { display: false } }, scales: defaultScales },
    }));
  }

  // Chart 3: Feeding interval distribution (Bar)
  const intervals = [];
  for (let i = 1; i < feedings.length; i++) {
    const diff = (C.getFullDateTime(feedings[i]) - C.getFullDateTime(feedings[i-1])) / 3600000;
    intervals.push(diff);
  }
  if (intervals.length > 0) {
    const buckets = {};
    intervals.forEach(h => {
      const key = h < 1 ? '~1h' : h < 2 ? '1~2h' : h < 3 ? '2~3h' : h < 4 ? '3~4h' : h < 5 ? '4~5h' : '5h+';
      buckets[key] = (buckets[key] || 0) + 1;
    });
    const labels = ['~1h','1~2h','2~3h','3~4h','4~5h','5h+'];
    charts.push(new Chart(el.querySelector('#chart3'), {
      type: 'bar',
      data: {
        labels,
        datasets: [{ label: '횟수', data: labels.map(l => buckets[l] || 0), backgroundColor: feedColor + 'AA' }],
      },
      options: { responsive: true, plugins: { legend: { display: false } }, scales: defaultScales },
    }));
  }

  // Chart 4: Daily bowel count (Grouped Bar)
  const bowelGroups = groupByDate(events.filter(e => e.category === '배변'));
  if (bowelGroups.length > 0) {
    charts.push(new Chart(el.querySelector('#chart4'), {
      type: 'bar',
      data: {
        labels: bowelGroups.map(g => g.label),
        datasets: [
          { label: '소변', data: bowelGroups.map(g => g.events.filter(e => e.hasUrine).length), backgroundColor: urineColor + 'AA' },
          { label: '대변', data: bowelGroups.map(g => g.events.filter(e => e.hasStool).length), backgroundColor: stoolColor + 'AA' },
        ],
      },
      options: { responsive: true, plugins: { legend: { labels: { color: textColor } } }, scales: defaultScales },
    }));
  }

  // Chart 5: Category pie
  const catCounts = {};
  events.forEach(e => { catCounts[e.category] = (catCounts[e.category] || 0) + 1; });
  const catLabels = Object.keys(catCounts);
  const catColors = catLabels.map(c => C.getCategoryColor(c));
  charts.push(new Chart(el.querySelector('#chart5'), {
    type: 'doughnut',
    data: {
      labels: catLabels,
      datasets: [{ data: catLabels.map(c => catCounts[c]), backgroundColor: catColors }],
    },
    options: { responsive: true, plugins: { legend: { position: 'bottom', labels: { color: textColor } } } },
  }));

  // Chart 6: Daily event status (Grouped Bar)
  const allDays = groupByDate(events);
  const cats = ['수유','배변','위생관리','신체측정','건강관리','기타'];
  const catColorMap = cats.map(c => C.getCategoryColor(c));
  charts.push(new Chart(el.querySelector('#chart6'), {
    type: 'bar',
    data: {
      labels: allDays.map(g => g.label),
      datasets: cats.map((c, i) => ({
        label: c,
        data: allDays.map(g => g.events.filter(e => e.category === c).length),
        backgroundColor: catColorMap[i] + 'AA',
      })),
    },
    options: { responsive: true, plugins: { legend: { labels: { color: textColor, font: { size: 11 } } } }, scales: defaultScales },
  }));
}

function chartCard(title, canvasId) {
  return `<div class="chart-card"><h3>${title}</h3><canvas id="${canvasId}"></canvas></div>`;
}

function groupByDate(events) {
  const map = {};
  events.forEach(e => {
    if (!e.date) return;
    const key = e.date.toISOString().slice(0, 10);
    if (!map[key]) map[key] = { label: key.slice(5).replace('-','/'), date: e.date, events: [] };
    map[key].events.push(e);
  });
  return Object.values(map).sort((a, b) => a.date - b.date);
}

function fmtShort(d) {
  if (!d) return '';
  return `${String(d.getMonth()+1)}/${String(d.getDate())} ${d.toTimeString().slice(0,5)}`;
}

function destroyCharts() {
  charts.forEach(c => c.destroy());
  charts = [];
}
