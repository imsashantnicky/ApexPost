(function () {
    const DATABASE_NAME = 'ApexPostBrowserStorage';
    const DATABASE_VERSION = 1;
    const STORE_NAMES = {
        drafts: 'drafts',
        queue: 'queue'
    };

    function isBrowserContext() {
        return typeof window !== 'undefined' && typeof indexedDB !== 'undefined';
    }

    function isSensitivePayload(payload) {
        if (!payload || typeof payload !== 'object') {
            return false;
        }

        const sensitiveKeys = [
            'password',
            'token',
            'authorization',
            'auth',
            'cookie',
            'secret',
            'apiKey',
            'sessionId',
            'personalId',
            'ssn'
        ];

        return Object.keys(payload).some((key) => sensitiveKeys.includes(key.toLowerCase()));
    }

    function createId() {
        if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
            return crypto.randomUUID();
        }

        return 'apexpost-' + Date.now() + '-' + Math.random().toString(16).slice(2);
    }

    function openDatabase() {
        return new Promise((resolve, reject) => {
            if (!isBrowserContext()) {
                reject(new Error('IndexedDB is not available in this environment.'));
                return;
            }

            const request = indexedDB.open(DATABASE_NAME, DATABASE_VERSION);

            request.onupgradeneeded = function (event) {
                const database = event.target.result;

                if (!database.objectStoreNames.contains(STORE_NAMES.drafts)) {
                    const drafts = database.createObjectStore(STORE_NAMES.drafts, { keyPath: 'id' });
                    drafts.createIndex('kind', 'kind', { unique: false });
                    drafts.createIndex('updatedAt', 'updatedAt', { unique: false });
                }

                if (!database.objectStoreNames.contains(STORE_NAMES.queue)) {
                    const queue = database.createObjectStore(STORE_NAMES.queue, { keyPath: 'id' });
                    queue.createIndex('state', 'state', { unique: false });
                    queue.createIndex('kind', 'kind', { unique: false });
                    queue.createIndex('createdAt', 'createdAt', { unique: false });
                }
            };

            request.onsuccess = function () {
                resolve(request.result);
            };

            request.onerror = function () {
                reject(request.error || new Error('IndexedDB open failed.'));
            };
        });
    }

    async function withDatabase(operation) {
        const database = await openDatabase();
        try {
            return await operation(database);
        } finally {
            database.close();
        }
    }

    function ensurePayloadIsSafe(kind, payload) {
        if (!kind || typeof kind !== 'string') {
            throw new Error('A storage kind is required.');
        }

        if (payload === undefined) {
            throw new Error('Storage payload is required.');
        }

        if (typeof payload !== 'object' || Array.isArray(payload)) {
            throw new Error('Storage payload must be an object.');
        }

        if (isSensitivePayload(payload)) {
            throw new Error('Sensitive values are not allowed in browser storage.');
        }
    }

    async function saveDraft(kind, payload) {
        ensurePayloadIsSafe(kind, payload);

        const draft = {
            id: createId(),
            kind,
            payload,
            updatedAt: new Date().toISOString(),
            createdAt: new Date().toISOString()
        };

        return withDatabase((database) => new Promise((resolve, reject) => {
            const transaction = database.transaction(STORE_NAMES.drafts, 'readwrite');
            const store = transaction.objectStore(STORE_NAMES.drafts);
            const request = store.put(draft);

            request.onsuccess = function () {
                resolve(draft);
            };

            request.onerror = function () {
                reject(request.error || new Error('Draft save failed.'));
            };
        }));
    }

    async function listDrafts(kind) {
        return withDatabase((database) => new Promise((resolve, reject) => {
            const transaction = database.transaction(STORE_NAMES.drafts, 'readonly');
            const store = transaction.objectStore(STORE_NAMES.drafts);
            const request = kind ? store.index('kind').getAll(IDBKeyRange.only(kind)) : store.getAll();

            request.onsuccess = function () {
                resolve(request.result || []);
            };

            request.onerror = function () {
                reject(request.error || new Error('Draft lookup failed.'));
            };
        }));
    }

    async function deleteDraft(id) {
        if (!id) {
            return false;
        }

        return withDatabase((database) => new Promise((resolve, reject) => {
            const transaction = database.transaction(STORE_NAMES.drafts, 'readwrite');
            const store = transaction.objectStore(STORE_NAMES.drafts);
            const request = store.delete(id);

            request.onsuccess = function () {
                resolve(true);
            };

            request.onerror = function () {
                reject(request.error || new Error('Draft delete failed.'));
            };
        }));
    }

    async function enqueueOperation(kind, payload, metadata) {
        ensurePayloadIsSafe(kind, payload);

        const operation = {
            id: createId(),
            kind,
            state: 'pending',
            payload: {
                ...payload,
                ...(metadata || {})
            },
            serverConfirmed: false,
            createdAt: new Date().toISOString(),
            updatedAt: new Date().toISOString(),
            retryCount: 0
        };

        return withDatabase((database) => new Promise((resolve, reject) => {
            const transaction = database.transaction(STORE_NAMES.queue, 'readwrite');
            const store = transaction.objectStore(STORE_NAMES.queue);
            const request = store.put(operation);

            request.onsuccess = function () {
                resolve(operation);
            };

            request.onerror = function () {
                reject(request.error || new Error('Queued operation write failed.'));
            };
        }));
    }

    async function listQueuedOperations(state) {
        return withDatabase((database) => new Promise((resolve, reject) => {
            const transaction = database.transaction(STORE_NAMES.queue, 'readonly');
            const store = transaction.objectStore(STORE_NAMES.queue);
            const request = state ? store.index('state').getAll(IDBKeyRange.only(state)) : store.getAll();

            request.onsuccess = function () {
                resolve(request.result || []);
            };

            request.onerror = function () {
                reject(request.error || new Error('Queue lookup failed.'));
            };
        }));
    }

    async function markQueueState(id, state, serverConfirmed) {
        if (!id || !state) {
            return false;
        }

        return withDatabase((database) => new Promise((resolve, reject) => {
            const transaction = database.transaction(STORE_NAMES.queue, 'readwrite');
            const store = transaction.objectStore(STORE_NAMES.queue);
            const request = store.get(id);

            request.onsuccess = function () {
                const record = request.result;
                if (!record) {
                    resolve(false);
                    return;
                }

                record.state = state;
                record.serverConfirmed = Boolean(serverConfirmed);
                record.updatedAt = new Date().toISOString();

                const updateRequest = store.put(record);
                updateRequest.onsuccess = function () {
                    resolve(true);
                };
                updateRequest.onerror = function () {
                    reject(updateRequest.error || new Error('Queue state update failed.'));
                };
            };

            request.onerror = function () {
                reject(request.error || new Error('Queue state lookup failed.'));
            };
        }));
    }

    async function getOfflineStatus() {
        const isOnline = typeof navigator !== 'undefined' ? navigator.onLine : true;

        try {
            const pending = await listQueuedOperations('pending');
            return {
                isOnline,
                pendingCount: pending.length,
                queueState: pending.length > 0 ? 'pending' : 'idle',
                storageSupported: isBrowserContext()
            };
        } catch (error) {
            return {
                isOnline,
                pendingCount: 0,
                queueState: 'unavailable',
                storageSupported: false,
                error: 'storage unavailable'
            };
        }
    }

    function updateStatusMarkup(element, queueElement) {
        if (!element) {
            return;
        }

        const setStatus = function (text, cssClass) {
            element.textContent = text;
            element.classList.remove('is-offline', 'is-unsupported');
            if (cssClass) {
                element.classList.add(cssClass);
            }
        };

        if (!isBrowserContext()) {
            setStatus('Browser storage is unavailable in this context.', 'is-unsupported');
            if (queueElement) {
                queueElement.textContent = 'IndexedDB unavailable';
            }
            return;
        }

        const onlineLabel = navigator.onLine ? 'Online' : 'Offline';
        const pendingText = queueElement ? queueElement : null;

        if (!navigator.onLine) {
            setStatus('Offline mode: browser storage remains available for local queue items only.', 'is-offline');
            if (pendingText) {
                pendingText.textContent = 'Pending local queue items are not server-confirmed.';
            }
            return;
        }

        setStatus('Browser storage is active and online. Server-confirmed records remain separate from local queue items.', 'is-online');
        if (pendingText) {
            pendingText.textContent = 'Online: local queue items are ready for sync when connected.';
        }
    }

    async function initializeOfflineIndicator() {
        const statusElement = document.getElementById('offline-status');
        const queueElement = document.getElementById('offline-queue-indicator');

        if (!statusElement && !queueElement) {
            return;
        }

        if (!isBrowserContext()) {
            updateStatusMarkup(statusElement, queueElement);
            return;
        }

        const status = await getOfflineStatus();

        if (statusElement) {
            statusElement.textContent = status.isOnline
                ? 'Browser storage is active. Pending queue items are only local until synchronized.'
                : 'Offline mode: pending queue items are stored locally and are not server-confirmed.';

            statusElement.classList.toggle('is-offline', !status.isOnline);
            statusElement.classList.toggle('is-unsupported', !status.storageSupported);
        }

        if (queueElement) {
            queueElement.textContent = status.pendingCount > 0
                ? 'Pending local queue: ' + status.pendingCount + ' item(s) not yet confirmed by the server.'
                : 'No pending local queue items.';
        }

        window.addEventListener('online', function () {
            if (statusElement) {
                statusElement.textContent = 'Back online: local queue items may sync when the next server request is allowed.';
                statusElement.classList.remove('is-offline');
            }
        });

        window.addEventListener('offline', function () {
            if (statusElement) {
                statusElement.textContent = 'Offline mode: pending queue items are stored locally and are not server-confirmed.';
                statusElement.classList.add('is-offline');
            }
        });
    }

    window.ApexPostBrowserStorage = {
        DATABASE_NAME,
        DATABASE_VERSION,
        STORE_NAMES,
        saveDraft,
        listDrafts,
        deleteDraft,
        enqueueOperation,
        listQueuedOperations,
        markQueueState,
        getOfflineStatus,
        initializeOfflineIndicator,
        openDatabase
    };

    document.addEventListener('DOMContentLoaded', function () {
        if (document.getElementById('offline-status') || document.getElementById('offline-queue-indicator')) {
            initializeOfflineIndicator();
        }
    }, { once: true });
})();
