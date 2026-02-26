// ─────────────────────────────────────────────────────────
// FCM Initialization — requests permission via user gesture,
// gets token, and registers it with the MobileOpsConnect server.
//
// iOS Safari requires Notification.requestPermission() to be
// triggered by a user gesture (button click). This script
// exposes window.enablePushNotifications() for the banner.
// ─────────────────────────────────────────────────────────
(function () {
    'use strict';

    // Firebase config — injected by the server via _Layout.cshtml
    const firebaseConfig = window.__FIREBASE_CONFIG__;
    const VAPID_KEY = window.__FIREBASE_VAPID_KEY__;

    if (!firebaseConfig || !VAPID_KEY) {
        console.warn('[FCM] Firebase config not found.');
        return;
    }

    if (!('Notification' in window) || !('serviceWorker' in navigator)) {
        console.warn('[FCM] This browser does not support push notifications.');
        return;
    }

    // Initialize Firebase
    firebase.initializeApp(firebaseConfig);
    const messaging = firebase.messaging();

    // ── Core: request permission + register token ──
    async function requestAndRegister() {
        try {
            const registration = await navigator.serviceWorker.ready;
            console.log('[FCM] Service worker ready:', registration.scope);

            const permission = await Notification.requestPermission();
            if (permission !== 'granted') {
                console.warn('[FCM] Notification permission denied.');
                return false;
            }
            console.log('[FCM] Notification permission granted.');

            const token = await messaging.getToken({
                vapidKey: VAPID_KEY,
                serviceWorkerRegistration: registration,
            });

            if (!token) {
                console.warn('[FCM] Failed to get FCM token.');
                return false;
            }
            console.log('[FCM] Token obtained:', token.substring(0, 20) + '...');

            const response = await fetch('/Notification/RegisterToken', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ token: token }),
            });

            if (response.ok) {
                const data = await response.json();
                console.log('[FCM] Token registered with server:', data.message);
            } else {
                console.warn('[FCM] Failed to register token:', response.status);
            }

            return true;
        } catch (error) {
            console.error('[FCM] Error during initialization:', error);
            return false;
        }
    }

    // ── Public function for the "Enable Notifications" banner ──
    window.enablePushNotifications = async function () {
        const success = await requestAndRegister();
        if (success) {
            // Hide the banner and remember the choice
            localStorage.setItem('moc_push_enabled', 'true');
            const banner = document.getElementById('push-banner');
            if (banner) {
                banner.style.transition = 'opacity 0.3s, transform 0.3s';
                banner.style.opacity = '0';
                banner.style.transform = 'translateY(-100%)';
                setTimeout(() => banner.remove(), 300);
            }
        }
    };

    // ── Dismiss banner without enabling ──
    window.dismissPushBanner = function () {
        localStorage.setItem('moc_push_dismissed', 'true');
        const banner = document.getElementById('push-banner');
        if (banner) {
            banner.style.transition = 'opacity 0.3s, transform 0.3s';
            banner.style.opacity = '0';
            banner.style.transform = 'translateY(-100%)';
            setTimeout(() => banner.remove(), 300);
        }
    };

    // ── Handle foreground messages ──
    function listenForMessages() {
        messaging.onMessage(function (payload) {
            console.log('[FCM] Foreground message received:', payload);
            showInAppToast(payload);
        });
    }

    // ── In-app toast for foreground messages ──
    function showInAppToast(payload) {
        const container = document.getElementById('toast-container');
        if (!container) return;

        const title = payload.notification?.title || 'MobileOps Connect';
        const body = payload.notification?.body || '';
        const url = payload.data?.url || '';
        const toastId = 'toast-' + Math.random().toString(36).substr(2, 9);

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

        const temp = document.createElement('div');
        temp.innerHTML = toastHtml;
        const toastElement = temp.firstElementChild;
        container.appendChild(toastElement);

        requestAnimationFrame(() => {
            toastElement.classList.remove('translate-x-full', 'opacity-0');
        });

        setTimeout(() => {
            if (document.getElementById(toastId)) {
                toastElement.classList.add('translate-x-full', 'opacity-0');
                setTimeout(() => toastElement.remove(), 300);
            }
        }, 6000);
    }

    // ── Startup ──
    function init() {
        listenForMessages();

        // If permission was already granted, silently register token (no banner needed)
        if (Notification.permission === 'granted') {
            requestAndRegister();
            return;
        }

        // If user previously dismissed or enabled, don't show banner again
        if (localStorage.getItem('moc_push_enabled') === 'true' ||
            localStorage.getItem('moc_push_dismissed') === 'true') {
            return;
        }

        // Show the banner (it exists in _Layout.cshtml, hidden by default)
        setTimeout(() => {
            const banner = document.getElementById('push-banner');
            if (banner) {
                banner.classList.remove('hidden');
                banner.classList.add('push-banner-show');
            }
        }, 2000); // slight delay so it doesn't compete with page load
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
