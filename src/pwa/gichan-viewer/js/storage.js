// IndexedDB storage for event data + LocalStorage for settings

const DB_NAME = 'gichan-viewer';
const DB_VERSION = 1;
const STORE_NAME = 'events';

let dbInstance = null;

function openDB() {
  if (dbInstance) return Promise.resolve(dbInstance);
  return new Promise((resolve, reject) => {
    const req = indexedDB.open(DB_NAME, DB_VERSION);
    req.onupgradeneeded = (e) => {
      const db = e.target.result;
      if (!db.objectStoreNames.contains(STORE_NAME)) {
        db.createObjectStore(STORE_NAME, { autoIncrement: true });
      }
    };
    req.onsuccess = (e) => { dbInstance = e.target.result; resolve(dbInstance); };
    req.onerror = (e) => reject(e.target.error);
  });
}

export async function saveEvents(events) {
  const db = await openDB();
  return new Promise((resolve, reject) => {
    const tx = db.transaction(STORE_NAME, 'readwrite');
    const store = tx.objectStore(STORE_NAME);
    store.clear();
    events.forEach(evt => store.add(evt));
    tx.oncomplete = () => {
      setSetting('lastImportDate', new Date().toISOString());
      setSetting('lastImportCount', events.length);
      resolve();
    };
    tx.onerror = (e) => reject(e.target.error);
  });
}

export async function loadEvents() {
  const db = await openDB();
  return new Promise((resolve, reject) => {
    const tx = db.transaction(STORE_NAME, 'readonly');
    const store = tx.objectStore(STORE_NAME);
    const req = store.getAll();
    req.onsuccess = () => resolve(req.result || []);
    req.onerror = (e) => reject(e.target.error);
  });
}

// LocalStorage helpers
export function getSetting(key, defaultVal = null) {
  const v = localStorage.getItem(`gv_${key}`);
  if (v === null) return defaultVal;
  try { return JSON.parse(v); } catch { return v; }
}

export function setSetting(key, value) {
  localStorage.setItem(`gv_${key}`, JSON.stringify(value));
}
