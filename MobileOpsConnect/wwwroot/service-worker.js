// ─── Firebase Cloud Messaging ───
let _firebaseInitialized = false;
let _pushHandledByFirebase = false;

try {
    importScripts('https://www.gstatic.com/firebasejs/10.12.0/firebase-app-compat.js');
    importScripts('https://www.gstatic.com/firebasejs/10.12.0/firebase-messaging-compat.js');

    firebase.initializeApp({
        apiKey: "AIzaSyAL81jGL4I-RRdhk8K-niHwUMOYTQ91kkQ",
        authDomain: "mobileops-connect.firebaseapp.com",
        projectId: "mobileops-connect",
        storageBucket: "mobileops-connect.firebasestorage.app",
        messagingSenderId: "800744847836",
        appId: "1:800744847836:web:bd9a01a4cc81740ef97719"
    });

    const messaging = firebase.messaging();
    _firebaseInitialized = true;

    // Firebase background handler (works on Chrome/Android)
    messaging.onBackgroundMessage(function (payload) {
        console.log('[SW] Firebase background message:', payload);
        _pushHandledByFirebase = true;

        const title = payload.notification?.title || 'MobileOps Connect';
        self.registration.showNotification(title, {
            body: payload.notification?.body || '',
            icon: '/icons/icon-192.png',
            badge: '/icons/icon-192.png',
            data: payload.data,
            tag: 'moc-' + Date.now(),
        });
    });
} catch (e) {
    console.warn('[SW] Firebase SDK not available, using standard push only:', e.message);
}

// ─── Standard Web Push fallback (required for iOS Safari) ───
self.addEventListener('push', function (event) {
    // If Firebase already handled this push, skip
    if (_pushHandledByFirebase) {
        _pushHandledByFirebase = false;
        return;
    }

    console.log('[SW] Standard push event received');

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
            // FCM sends { notification: { title, body }, data: { ... } }
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
const CACHE_NAME = 'mobileops-v3';
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

    // Skip SignalR, Firebase, and external requests
    if (request.url.includes('/hubs/') ||
        request.url.includes('firebaseio.com') ||
        request.url.includes('googleapis.com') ||
        request.url.includes('fcm/send')) {
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
