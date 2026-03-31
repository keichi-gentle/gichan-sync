// Role management — admin/editor/observer

import { getCurrentUser } from './firebase-auth.js';

let currentRole = null;
let rolesData = null;

export async function loadRoles(db) {
  const { doc, getDoc } = await import('https://www.gstatic.com/firebasejs/10.12.0/firebase-firestore.js');
  const snap = await getDoc(doc(db, 'config', 'roles'));
  if (snap.exists()) {
    rolesData = snap.data();
  }
  return rolesData;
}

export function determineRole(email) {
  if (!rolesData || !email) return null;

  const toList = (arr) => {
    if (!arr) return [];
    if (Array.isArray(arr)) return arr;
    return [];
  };

  if (toList(rolesData.admin).includes(email)) { currentRole = 'admin'; return 'admin'; }
  if (toList(rolesData.editor).includes(email)) { currentRole = 'editor'; return 'editor'; }
  if (toList(rolesData.observer).includes(email)) { currentRole = 'observer'; return 'observer'; }
  return null; // Not registered
}

export function getRole() { return currentRole; }
export function getRolesData() { return rolesData; }

export function isAdmin() { return currentRole === 'admin'; }
export function isEditor() { return currentRole === 'editor'; }
export function isObserver() { return currentRole === 'observer'; }
export function canWrite() { return currentRole === 'admin' || currentRole === 'editor'; }
export function canManageUsers() { return currentRole === 'admin'; }

// ── Admin: save roles to Firestore ──
export async function saveRoles(db, newRolesData) {
  const { doc, setDoc, Timestamp } = await import('https://www.gstatic.com/firebasejs/10.12.0/firebase-firestore.js');
  await setDoc(doc(db, 'config', 'roles'), {
    ...newRolesData,
    updatedAt: Timestamp.now(),
  });
  rolesData = newRolesData;
}
