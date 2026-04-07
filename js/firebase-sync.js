// Firestore real-time sync — subscribe to user's events collection
import { setSyncStatus } from './scoreboard.js';

let unsubscribe = null;

export async function subscribeToEvents(db, userId, onEventsChanged) {
  // Unsubscribe previous listener if any
  if (unsubscribe) { unsubscribe(); unsubscribe = null; }

  const { collection, query, orderBy, onSnapshot } = await import('https://www.gstatic.com/firebasejs/10.12.0/firebase-firestore.js');

  const eventsRef = collection(db, 'users', userId, 'events');
  const q = query(eventsRef, orderBy('date', 'asc'));

  unsubscribe = onSnapshot(q, (snapshot) => {
    const events = [];
    snapshot.forEach((doc) => {
      const d = doc.data();
      events.push(mapFirestoreToEvent(d));
    });
    onEventsChanged(events);
  }, (error) => {
    console.error('Firestore listener error:', error);
    setSyncStatus(false);
  });
}

export function unsubscribeEvents() {
  if (unsubscribe) { unsubscribe(); unsubscribe = null; }
}

function mapFirestoreToEvent(d) {
  return {
    id: d.id || null,
    dayNumber: d.dayNumber ?? null,
    date: d.date?.toDate ? d.date.toDate() : parseDate(d.date),
    time: d.time || null,
    category: mapCategory(d.category),
    detail: d.detail || '',
    amount: d.amount || '-',
    note: d.note || '',
    feedingInterval: d.feedingInterval || null,
    nextExpected: d.nextExpected || null,
    dailyFeedTotal: d.dailyFeedTotal ?? null,
    formulaProduct: d.formulaProduct || null,
    formulaAmount: d.formulaAmount ?? null,
    breastfeedAmount: d.breastfeedAmount ?? null,
    feedingCount: d.feedingCount ?? null,
    hasUrine: d.hasUrine ?? null,
    hasStool: d.hasStool ?? null,
    immediateNotice: d.immediateNotice ?? null,
  };
}

const CATEGORY_MAP = {
  '수유': '수유', '배변': '배변',
  '위생관리': '위생관리', '위생': '위생관리',
  '신체측정': '신체측정', '신체': '신체측정',
  '건강관리': '건강관리', '통증': '건강관리',
  '기타': '기타',
};

function mapCategory(cat) {
  return CATEGORY_MAP[cat] || '기타';
}

function parseDate(val) {
  if (!val) return new Date();
  if (val instanceof Date) return val;
  return new Date(val);
}
