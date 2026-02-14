// Firebase Messaging Service Worker
// This file MUST be at the root of wwwroot (i.e., served from /)

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

// Handle background messages (when the browser tab is not focused)
messaging.onBackgroundMessage(function (payload) {
    console.log('[SW] Background message received:', payload);

    const notificationTitle = payload.notification?.title || 'MobileOpsConnect';
    const notificationOptions = {
        body: payload.notification?.body || '',
        icon: '/favicon.ico',
        badge: '/favicon.ico',
        data: payload.data,
        tag: 'moc-notification', // Prevents duplicate notifications
    };

    self.registration.showNotification(notificationTitle, notificationOptions);
});

// Handle notification click — navigate to the app
self.addEventListener('notificationclick', function (event) {
    event.notification.close();

    const url = event.notification.data?.url || '/';

    event.waitUntil(
        clients.matchAll({ type: 'window', includeUncontrolled: true }).then(function (clientList) {
            // If a window is already open, focus it
            for (const client of clientList) {
                if (client.url.includes(self.location.origin) && 'focus' in client) {
                    return client.focus();
                }
            }
            // Otherwise open a new window
            return clients.openWindow(url);
        })
    );
});
