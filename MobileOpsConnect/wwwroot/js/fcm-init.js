// ─────────────────────────────────────────────────────────
// FCM Initialization — requests permission, gets token,
// and registers it with the MobileOpsConnect server.
// ─────────────────────────────────────────────────────────
(function () {
    'use strict';

    // Firebase config (must match firebase-messaging-sw.js)
    const firebaseConfig = {
        apiKey: "AIzaSyAL81jGL4I-RRdhk8K-niHwUMOYTQ91kkQ",
        authDomain: "mobileops-connect.firebaseapp.com",
        projectId: "mobileops-connect",
        storageBucket: "mobileops-connect.firebasestorage.app",
        messagingSenderId: "800744847836",
        appId: "1:800744847836:web:bd9a01a4cc81740ef97719"
    };

    const VAPID_KEY = 'BO3HeUvBl3pbv-2EKspH6NF2VVobx3tXQ2AnWXL_NywRZC6prbLMtNNrGMF6VzikJFiQAicjy11ijePmavEpyWk';

    // Only run if the browser supports notifications and service workers
    if (!('Notification' in window) || !('serviceWorker' in navigator)) {
        console.warn('[FCM] This browser does not support push notifications.');
        return;
    }

    // Initialize Firebase
    firebase.initializeApp(firebaseConfig);
    const messaging = firebase.messaging();

    // Get the anti-forgery token from the page (ASP.NET Core convention)
    function getAntiForgeryToken() {
        const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
        if (tokenInput) return tokenInput.value;

        const metaToken = document.querySelector('meta[name="csrf-token"]');
        if (metaToken) return metaToken.getAttribute('content');

        return null;
    }

    // Register the service worker, then request permission & get token
    async function initFcm() {
        try {
            // 1. Register the service worker
            const registration = await navigator.serviceWorker.register('/firebase-messaging-sw.js');
            console.log('[FCM] Service worker registered:', registration.scope);

            // 2. Request notification permission
            const permission = await Notification.requestPermission();
            if (permission !== 'granted') {
                console.warn('[FCM] Notification permission denied.');
                return;
            }
            console.log('[FCM] Notification permission granted.');

            // 3. Get the FCM token
            const token = await messaging.getToken({
                vapidKey: VAPID_KEY,
                serviceWorkerRegistration: registration,
            });

            if (!token) {
                console.warn('[FCM] Failed to get FCM token.');
                return;
            }
            console.log('[FCM] Token obtained:', token.substring(0, 20) + '...');

            // 4. Send the token to the server
            const antiForgeryToken = getAntiForgeryToken();
            const headers = { 'Content-Type': 'application/json' };
            if (antiForgeryToken) {
                headers['RequestVerificationToken'] = antiForgeryToken;
            }

            const response = await fetch('/Notification/RegisterToken', {
                method: 'POST',
                headers: headers,
                body: JSON.stringify({ token: token }),
            });

            if (response.ok) {
                const data = await response.json();
                console.log('[FCM] Token registered with server:', data.message);
            } else {
                console.warn('[FCM] Failed to register token:', response.status);
            }
        } catch (error) {
            console.error('[FCM] Error during initialization:', error);
        }
    }

    // Handle foreground messages (when the tab IS focused)
    function listenForMessages() {
        messaging.onMessage(function (payload) {
            console.log('[FCM] Foreground message received:', payload);

            // Show a browser notification even when the tab is focused
            if (Notification.permission === 'granted') {
                new Notification(payload.notification?.title || 'MobileOpsConnect', {
                    body: payload.notification?.body || '',
                    icon: '/favicon.ico',
                });
            }
        });
    }

    // Run when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function () {
            initFcm();
            listenForMessages();
        });
    } else {
        initFcm();
        listenForMessages();
    }
})();
