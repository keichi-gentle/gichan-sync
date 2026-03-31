// Firebase Authentication — Google sign-in

let auth = null;
let currentUser = null;
let onAuthChangeCallback = null;

export async function initAuth(firebaseAuth, googleProvider) {
  auth = firebaseAuth;

  const { onAuthStateChanged } = await import('https://www.gstatic.com/firebasejs/10.12.0/firebase-auth.js');

  onAuthStateChanged(auth, (user) => {
    currentUser = user;
    if (onAuthChangeCallback) onAuthChangeCallback(user);
  });
}

export function onAuthChange(callback) {
  onAuthChangeCallback = callback;
  // Fire immediately if already resolved
  if (currentUser !== undefined) callback(currentUser);
}

export async function signIn() {
  const { GoogleAuthProvider, signInWithPopup } = await import('https://www.gstatic.com/firebasejs/10.12.0/firebase-auth.js');
  const provider = new GoogleAuthProvider();
  try {
    const result = await signInWithPopup(auth, provider);
    return result.user;
  } catch (err) {
    console.error('Sign-in failed:', err);
    throw err;
  }
}

export async function signOut() {
  if (auth) {
    const { signOut: fbSignOut } = await import('https://www.gstatic.com/firebasejs/10.12.0/firebase-auth.js');
    await fbSignOut(auth);
    currentUser = null;
  }
}

export function getCurrentUser() {
  return currentUser;
}
