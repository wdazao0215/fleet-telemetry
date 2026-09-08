import type { LocalAlert, QueuedReading } from "../domain/types";

const DATABASE_NAME = "fleet-driver";
const DATABASE_VERSION = 1;
const READINGS_STORE = "readings";
const ALERTS_STORE = "alerts";

/**
 * Persistencia local del cliente del conductor, sobre IndexedDB.
 *
 * IndexedDB y no localStorage por dos razones: la cola puede acumular miles de lecturas tras una
 * desconexión larga (localStorage ronda los 5 MB y es síncrono, así que bloquearía la interfaz), y
 * hace falta poder borrar por lote lo que ya se confirmó sin reescribir todo el conjunto.
 */
function openDatabase(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    const request = indexedDB.open(DATABASE_NAME, DATABASE_VERSION);

    request.onupgradeneeded = () => {
      const database = request.result;

      if (!database.objectStoreNames.contains(READINGS_STORE)) {
        database.createObjectStore(READINGS_STORE, { keyPath: "id" });
      }

      if (!database.objectStoreNames.contains(ALERTS_STORE)) {
        database.createObjectStore(ALERTS_STORE, { keyPath: "id" });
      }
    };

    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });
}

function promisify<T>(request: IDBRequest<T>): Promise<T> {
  return new Promise((resolve, reject) => {
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });
}

async function withStore<T>(
  storeName: string,
  mode: IDBTransactionMode,
  action: (store: IDBObjectStore) => Promise<T> | T,
): Promise<T> {
  const database = await openDatabase();

  try {
    const transaction = database.transaction(storeName, mode);
    const result = await action(transaction.objectStore(storeName));

    await new Promise<void>((resolve, reject) => {
      transaction.oncomplete = () => resolve();
      transaction.onerror = () => reject(transaction.error);
      transaction.onabort = () => reject(transaction.error);
    });

    return result;
  } finally {
    database.close();
  }
}

export const outbox = {
  async enqueue(reading: QueuedReading): Promise<void> {
    await withStore(READINGS_STORE, "readwrite", (store) => promisify(store.put(reading)));
  },

  async all(): Promise<QueuedReading[]> {
    return withStore(READINGS_STORE, "readonly", (store) =>
      promisify(store.getAll() as IDBRequest<QueuedReading[]>),
    );
  },

  /** Elimina de golpe lo que el servidor ya confirmó. */
  async remove(ids: readonly string[]): Promise<void> {
    await withStore(READINGS_STORE, "readwrite", async (store) => {
      for (const id of ids) {
        await promisify(store.delete(id));
      }
    });
  },

  async markAttempted(readings: readonly QueuedReading[]): Promise<void> {
    await withStore(READINGS_STORE, "readwrite", async (store) => {
      for (const reading of readings) {
        await promisify(store.put({ ...reading, attempts: reading.attempts + 1 }));
      }
    });
  },

  async recordAlert(alert: LocalAlert): Promise<void> {
    await withStore(ALERTS_STORE, "readwrite", (store) => promisify(store.put(alert)));
  },

  async alerts(): Promise<LocalAlert[]> {
    const stored = await withStore(ALERTS_STORE, "readonly", (store) =>
      promisify(store.getAll() as IDBRequest<LocalAlert[]>),
    );

    return stored.sort((a, b) => b.at.localeCompare(a.at)).slice(0, 50);
  },
};
