// Firestore settings sync — read/write users/{uid}/settings/app

import { getCurrentUser } from './firebase-auth.js';
import { getSetting, setSetting } from './storage.js';
import { getRolesData } from './roles.js';

let unsubscribe = null;

export async function subscribeToSettings(db, userId, onSettingsChanged) {
  if (unsubscribe) { unsubscribe(); unsubscribe = null; }

  const { doc, onSnapshot } = await import('https://www.gstatic.com/firebasejs/10.12.0/firebase-firestore.js');
  const settingsRef = doc(db, 'users', userId, 'settings', 'app');

  unsubscribe = onSnapshot(settingsRef, (snap) => {
    if (snap.exists()) {
      const data = snap.data();
      // Cache to LocalStorage
      if (data.babyName) setSetting('babyName', data.babyName);
      if (data.fixedFeedingInterval) setSetting('fixedFeedingInterval', data.fixedFeedingInterval);
      if (data.averageFeedingCount) setSetting('averageFeedingCount', data.averageFeedingCount);
      if (data.formulaProducts) setSetting('formulaProducts', data.formulaProducts);
      if (data.defaultFormulaProduct) setSetting('defaultFormulaProduct', data.defaultFormulaProduct);
      if (data.defaultFormulaAmount) setSetting('defaultFormulaAmount', data.defaultFormulaAmount);
      if (data.defaultBreastfeedAmount) setSetting('defaultBreastfeedAmount', data.defaultBreastfeedAmount);
      if (onSettingsChanged) onSettingsChanged(data);
    }
  }, (error) => {
    console.error('Settings listener error:', error);
  });
}

export async function saveSettingsToFirestore(settings) {
  const user = getCurrentUser();
  if (!user) return;

  const { doc, setDoc, Timestamp } = await import('https://www.gstatic.com/firebasejs/10.12.0/firebase-firestore.js');
  const db = window.__firebase.db;
  const dataUid = getRolesData()?.dataUid || user.uid;
  const settingsRef = doc(db, 'users', dataUid, 'settings', 'app');

  await setDoc(settingsRef, {
    ...settings,
    updatedAt: Timestamp.now(),
  }, { merge: true });
}

export function unsubscribeSettings() {
  if (unsubscribe) { unsubscribe(); unsubscribe = null; }
}

// Default settings (matches WPF AppSettings)
export function getDefaultSettings() {
  return {
    babyName: getSetting('babyName', ''),
    fixedFeedingInterval: getSetting('fixedFeedingInterval', 10800), // 3 hours in seconds
    averageFeedingCount: getSetting('averageFeedingCount', 10),
    formulaProducts: getSetting('formulaProducts', ['트루맘 클래식']),
    defaultFormulaProduct: getSetting('defaultFormulaProduct', '트루맘 클래식'),
    defaultFormulaAmount: getSetting('defaultFormulaAmount', 100),
    defaultBreastfeedAmount: getSetting('defaultBreastfeedAmount', 20),
    pageSize: getSetting('pageSize', 30),
  };
}
