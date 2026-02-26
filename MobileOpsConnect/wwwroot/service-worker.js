// ─── Standard Web Push Handler ───
// Handles push notifications from the server via the standard Web Push protocol.
// Works on ALL browsers: Chrome, Firefox, Safari iOS.

self.addEventListener('push', function (event) {
    console.log('[SW] Push event received');

    let title = 'MobileOps Connect';
    let options = {
        body: '',
        icon: '/icons/icon-192.png',
        badge: '/icons/icon-192.png',
        tag: 'moc-' + Date.now(),
    };

    try {
        const payload = event.data?.json();
        if (payload) {
            title = payload.notification?.title || payload.data?.title || title;
            options.body = payload.notification?.body || payload.data?.body || '';
            options.data = payload.data || {};
        }
    } catch (e) {
        // If not JSON, use the text directly
        const text = event.data?.text();
        if (text) options.body = text;
    }

    event.waitUntil(
        self.registration.showNotification(title, options)
    );
});

// Handle notification click — navigate to app
self.addEventListener('notificationclick', function (event) {
    event.notification.close();
    const url = event.notification.data?.url || '/';
    event.waitUntil(
        clients.matchAll({ type: 'window', includeUncontrolled: true }).then(function (clientList) {
            for (const client of clientList) {
                if (client.url.includes(self.location.origin) && 'focus' in client) {
                    return client.focus();
                }
            }
            return clients.openWindow(url);
        })
    );
});

// ─── PWA Offline Support ───
const CACHE_NAME = 'mobileops-v4';
const OFFLINE_URL = '/offline.html';

const PRECACHE_ASSETS = [
    OFFLINE_URL,
    '/css/site.css',
    '/js/site.js',
    '/manifest.json'
];

// Install — pre-cache essential assets
self.addEventListener('install', (event) => {
    event.waitUntil(
        caches.open(CACHE_NAME).then((cache) => {
            console.log('[SW] Pre-caching offline page and static assets');
            return cache.addAll(PRECACHE_ASSETS);
        })
    );
    self.skipWaiting();
});

// Activate — clean old caches
self.addEventListener('activate', (event) => {
    event.waitUntil(
        caches.keys().then((cacheNames) => {
            return Promise.all(
                cacheNames
                    .filter((name) => name !== CACHE_NAME)
                    .map((name) => caches.delete(name))
            );
        })
    );
    self.clients.claim();
});

// Fetch — network-first for pages, cache-first for static assets
self.addEventListener('fetch', (event) => {
    const { request } = event;

    // Skip non-GET requests
    if (request.method !== 'GET') return;

    // Skip SignalR and external requests
    if (request.url.includes('/hubs/') ||
        request.url.includes('googleapis.com')) {
        return;
    }

    // Navigation requests — network first, fallback to offline page
    if (request.mode === 'navigate') {
        event.respondWith(
            fetch(request)
                .then((response) => {
                    const clone = response.clone();
                    caches.open(CACHE_NAME).then((cache) => cache.put(request, clone));
                    return response;
                })
                .catch(() => {
                    return caches.match(request).then((cached) => {
                        return cached || caches.match(OFFLINE_URL);
                    });
                })
        );
        return;
    }

    // Static assets — cache first, then network
    if (request.url.match(/\.(css|js|png|jpg|jpeg|gif|svg|woff2?|ttf|eot|ico)$/)) {
        event.respondWith(
            caches.match(request).then((cached) => {
                if (cached) return cached;

                return fetch(request).then((response) => {
                    const clone = response.clone();
                    caches.open(CACHE_NAME).then((cache) => cache.put(request, clone));
                    return response;
                });
            })
        );
        return;
    }

    // Default — network with cache fallback
    event.respondWith(
        fetch(request)
            .then((response) => {
                const clone = response.clone();
                caches.open(CACHE_NAME).then((cache) => cache.put(request, clone));
                return response;
            })
            .catch(() => caches.match(request))
    );
});
