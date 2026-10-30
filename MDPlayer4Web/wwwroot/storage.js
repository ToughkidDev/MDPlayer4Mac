// IndexedDB copy of the files WebPersistence.cs keeps (settings and driver files).
const DB = 'mdplayer4web';
const STORE = 'files';

function open() {
  return new Promise((resolve, reject) => {
    const request = indexedDB.open(DB, 2);
    request.onupgradeneeded = () => {
      const db = request.result;
      if (!db.objectStoreNames.contains(STORE)) db.createObjectStore(STORE);
    };
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });
}

let dbPromise = null;
const db = () => (dbPromise ??= open());

export async function loadAll() {
  try {
    const store = (await db()).transaction(STORE).objectStore(STORE);
    return await new Promise((resolve, reject) => {
      const out = [];
      const cursor = store.openCursor();
      cursor.onsuccess = () => {
        const c = cursor.result;
        if (!c) return resolve(out);
        out.push({ path: c.key, data: new Uint8Array(c.value) });
        c.continue();
      };
      cursor.onerror = () => reject(cursor.error);
    });
  } catch {
    return [];
  }
}

async function put(path, data) {
  try { (await db()).transaction(STORE, 'readwrite').objectStore(STORE).put(data, path); } catch { /* private mode */ }
}

async function remove(path) {
  try { (await db()).transaction(STORE, 'readwrite').objectStore(STORE).delete(path); } catch { /* private mode */ }
}

export const imports = {
  // data arrives as a Uint8Array view that is only valid during the call: copy it.
  persistFile: (path, data) => { put(path, data.slice().buffer); },
  forgetFile: (path) => { remove(path); },
};
