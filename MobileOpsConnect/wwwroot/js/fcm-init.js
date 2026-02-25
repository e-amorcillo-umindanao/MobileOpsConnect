// ─────────────────────────────────────────────────────────
// FCM Initialization — requests permission, gets token,
// and registers it with the MobileOpsConnect server.
// ─────────────────────────────────────────────────────────
(function () {
    'use strict';

    // Firebase config — injected by the server via _Layout.cshtml
    // (see window.__FIREBASE_CONFIG__ and window.__FIREBASE_VAPID_KEY__)
    const firebaseConfig = window.__FIREBASE_CONFIG__;
    const VAPID_KEY = window.__FIREBASE_VAPID_KEY__;

    if (!firebaseConfig || !VAPID_KEY) {
        console.warn('[FCM] Firebase config not found. Ensure server-side injection is working.');
        return;
    }

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
            showInAppToast(payload);
        });
    }

    // Displays a beautiful Tailwind UI toast inside the app
    function showInAppToast(payload) {
        const container = document.getElementById('toast-container');
        if (!container) return;

        const title = payload.notification?.title || 'MobileOps Connect';
        const body = payload.notification?.body || '';
        const url = payload.data?.url || '';

        // Generate unique ID for this toast
        const toastId = 'toast-' + Math.random().toString(36).substr(2, 9);

        // Build HTML string
        const toastHtml = `
            <div id="${toastId}" class="max-w-xs bg-white border border-gray-200 rounded-xl shadow-lg dark:bg-neutral-800 dark:border-neutral-700 pointer-events-auto transition-all duration-300 transform translate-x-full opacity-0" role="alert">
                <div class="flex p-4">
                    <div class="shrink-0">
                        <svg class="shrink-0 size-5 text-blue-600 dark:text-blue-500 mt-0.5" xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                            <path d="M12 22c1.7 0 3-1.3 3-3h-6c0 1.7 1.3 3 3 3Z"></path>
                            <path d="M19 17V11a7 7 0 0 0-14 0v6l-2 2h18l-2-2Z"></path>
                        </svg>
                    </div>
                    <div class="ms-3 cursor-pointer" onclick="${url ? `window.location.href='${url}'` : ''}">
                        <h3 class="text-sm font-semibold text-gray-800 dark:text-white">${title}</h3>
                        <p class="text-sm text-gray-500 dark:text-neutral-400 mt-1">${body}</p>
                    </div>
                    <div class="ms-auto mt-0.5">
                        <button type="button" class="inline-flex shrink-0 justify-center items-center size-5 rounded-lg text-gray-800 opacity-50 hover:opacity-100 focus:outline-none focus:opacity-100 dark:text-white" onclick="document.getElementById('${toastId}').remove()">
                            <span class="sr-only">Close</span>
                            <svg class="shrink-0 size-4" xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M18 6 6 18"></path><path d="m6 6 12 12"></path></svg>
                        </button>
                    </div>
                </div>
            </div>
        `;

        // Parse and append element
        const temp = document.createElement('div');
        temp.innerHTML = toastHtml;
        const toastElement = temp.firstElementChild;
        container.appendChild(toastElement);

        // Trigger slide-in animation
        requestAnimationFrame(() => {
            toastElement.classList.remove('translate-x-full', 'opacity-0');
        });

        // Auto remove after 6 seconds
        setTimeout(() => {
            if (document.getElementById(toastId)) {
                toastElement.classList.add('translate-x-full', 'opacity-0');
                setTimeout(() => toastElement.remove(), 300); // Wait for transition
            }
        }, 6000);
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
