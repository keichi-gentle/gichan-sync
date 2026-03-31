// Excel → BabyEvent[] parser using SheetJS

const CATEGORY_MAP = {
  '수유': '수유', '배변': '배변',
  '위생관리': '위생관리', '위생': '위생관리',
  '신체측정': '신체측정', '신체': '신체측정',
  '건강관리': '건강관리', '통증': '건강관리',
  '기타': '기타',
};

export function parseExcelFile(file) {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = (e) => {
      try {
        const data = new Uint8Array(e.target.result);
        const wb = XLSX.read(data, { type: 'array', cellDates: true });
        const ws = wb.Sheets[wb.SheetNames[0]];
        const rows = XLSX.utils.sheet_to_json(ws, { header: 1, defval: null, raw: false });
        const events = [];
        for (let i = 1; i < rows.length; i++) {
          const r = rows[i];
          if (!r || r.every(c => c == null || c === '')) continue;
          const evt = parseRow(r);
          if (evt) events.push(evt);
        }
        resolve(events);
      } catch (err) {
        reject(err);
      }
    };
    reader.onerror = reject;
    reader.readAsArrayBuffer(file);
  });
}

function parseRow(r) {
  const rawDate = r[1];
  const date = parseDate(rawDate);
  if (!date) return null;

  const rawCat = String(r[3] || '기타').trim();
  const category = CATEGORY_MAP[rawCat] || '기타';

  return {
    dayNumber: parseDayNumber(r[0]),
    date,
    time: parseTime(r[2]),
    category,
    detail: String(r[4] || ''),
    amount: String(r[5] || '-'),
    note: String(r[6] || ''),
    feedingInterval: r[7] ? String(r[7]) : null,
    nextExpected: r[8] ? String(r[8]) : null,
    dailyFeedTotal: parseNum(r[9]),
    formulaProduct: r[10] ? String(r[10]) : null,
    formulaAmount: parseNum(r[11]),
    breastfeedAmount: parseNum(r[12]),
    feedingCount: parseNum(r[13]),
    hasUrine: parseBool(r[14]),
    hasStool: parseBool(r[15]),
    immediateNotice: parseBool(r[16]),
  };
}

function parseDate(val) {
  if (!val) return null;
  if (val instanceof Date && !isNaN(val)) return val;
  const s = String(val).trim();
  // Try YYYY/MM/DD or YYYY-MM-DD
  const m = s.match(/^(\d{4})[\/\-](\d{1,2})[\/\-](\d{1,2})/);
  if (m) return new Date(+m[1], +m[2] - 1, +m[3]);
  // Excel serial number
  const n = Number(s);
  if (n > 30000 && n < 60000) {
    return new Date((n - 25569) * 86400000);
  }
  const d = new Date(s);
  return isNaN(d) ? null : d;
}

function parseTime(val) {
  if (!val) return null;
  const s = String(val).trim();
  // HH:mm or HH:mm:ss
  const m = s.match(/^(\d{1,2}):(\d{2})/);
  if (m) return `${m[1].padStart(2, '0')}:${m[2]}`;
  // Excel decimal time (e.g., 0.541666... = 13:00)
  const n = Number(s);
  if (n >= 0 && n < 1) {
    const totalMin = Math.round(n * 1440);
    const h = Math.floor(totalMin / 60);
    const min = totalMin % 60;
    return `${String(h).padStart(2, '0')}:${String(min).padStart(2, '0')}`;
  }
  return null;
}

function parseDayNumber(val) {
  if (!val) return null;
  const n = parseInt(String(val));
  return isNaN(n) ? null : n;
}

function parseNum(val) {
  if (val == null || val === '') return null;
  const n = Number(val);
  return isNaN(n) ? null : n;
}

function parseBool(val) {
  if (val == null || val === '') return null;
  const s = String(val).toUpperCase().trim();
  if (s === 'TRUE' || s === '1') return true;
  if (s === 'FALSE' || s === '0') return false;
  return null;
}
