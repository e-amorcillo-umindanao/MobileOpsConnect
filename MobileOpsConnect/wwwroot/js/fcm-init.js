// ─────────────────────────────────────────────────────────
// FCM + Standard Web Push Initialization
//
// This script handles BOTH:
// 1. FCM tokens (for Chrome/Android push via Firebase)
// 2. Standard Web Push subscriptions (for iOS Safari PWA)
//
// iOS Safari requires:
// - requestPermission() triggered by user gesture (button)
// - Standard PushManager.subscribe() with VAPID key
// ─────────────────────────────────────────────────────────
(function () {
    'use strict';

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

    firebase.initializeApp(firebaseConfig);
    const messaging = firebase.messaging();

    // ── Visible debug log (temporary — remove after push is working) ──
    function debugLog(msg) {
        console.log('[FCM] ' + msg);
        let el = document.getElementById('fcm-debug');
        if (!el) {
            el = document.createElement('div');
            el.id = 'fcm-debug';
            el.style.cssText = 'position:fixed;bottom:0;left:0;right:0;max-height:40vh;overflow-y:auto;background:rgba(0,0,0,0.85);color:#0f0;font:11px/1.4 monospace;padding:8px;z-index:99999;pointer-events:auto;';
            document.body.appendChild(el);
        }
        el.innerHTML += msg + '<br>';
        el.scrollTop = el.scrollHeight;
    }

    // Convert URL-safe base64 to Uint8Array (needed for PushManager.subscribe)
    function urlBase64ToUint8Array(base64String) {
        const padding = '='.repeat((4 - base64String.length % 4) % 4);
        const base64 = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/');
        const rawData = window.atob(base64);
        const outputArray = new Uint8Array(rawData.length);
        for (let i = 0; i < rawData.length; ++i) {
            outputArray[i] = rawData.charCodeAt(i);
        }
        return outputArray;
    }

    // ── Register FCM token (works on Chrome/Android) ──
    async function registerFcmToken(registration) {
        try {
            debugLog('⏳ Getting FCM token...');
            const token = await messaging.getToken({
                vapidKey: VAPID_KEY,
                serviceWorkerRegistration: registration,
            });
            if (!token) {
                debugLog('⚠️ No FCM token returned');
                return;
            }
            debugLog('✅ FCM Token: ' + token.substring(0, 25) + '...');

            const response = await fetch('/Notification/RegisterToken', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ token: token }),
            });
            if (response.ok) {
                debugLog('✅ FCM token registered');
            } else {
                debugLog('⚠️ FCM register failed: HTTP ' + response.status);
            }
        } catch (e) {
            debugLog('⚠️ FCM token error: ' + e.message);
        }
    }

    // ── Register standard Web Push subscription (works on iOS Safari) ──
    async function registerWebPushSubscription(registration) {
        try {
            // Get the VAPID public key from the server
            debugLog('⏳ Getting VAPID key...');
            const vapidResponse = await fetch('/Notification/GetVapidKey');
            if (!vapidResponse.ok) {
                debugLog('⚠️ VAPID key fetch failed');
                return;
            }
            const { publicKey } = await vapidResponse.json();
            if (!publicKey) {
                debugLog('⚠️ No VAPID public key configured');
                return;
            }
            debugLog('✅ VAPID key: ' + publicKey.substring(0, 20) + '...');

            // Subscribe using the standard Push API
            debugLog('⏳ Creating Web Push subscription...');
            const subscription = await registration.pushManager.subscribe({
                userVisibleOnly: true,
                applicationServerKey: urlBase64ToUint8Array(publicKey),
            });
            debugLog('✅ Web Push subscribed');

            // Extract the subscription details
            const subJson = subscription.toJSON();
            const payload = {
                endpoint: subJson.endpoint,
                p256dh: subJson.keys?.p256dh ?? '',
                auth: subJson.keys?.auth ?? '',
            };
            debugLog('📡 Endpoint: ' + payload.endpoint.substring(0, 50) + '...');

            // Send to server
            debugLog('⏳ Registering subscription with server...');
            const response = await fetch('/Notification/RegisterSubscription', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload),
            });
            if (response.ok) {
                debugLog('✅ Web Push subscription registered!');
            } else {
                debugLog('❌ Subscription register failed: HTTP ' + response.status);
            }
        } catch (e) {
            debugLog('❌ Web Push error: ' + e.message);
        }
    }

    // ── Full registration (FCM + Web Push) — no permission request ──
    async function registerTokenOnly() {
        try {
            debugLog('⏳ Waiting for service worker...');
            const registration = await navigator.serviceWorker.ready;
            debugLog('✅ SW ready: ' + registration.scope);

            // Register BOTH FCM token and standard Web Push subscription
            await Promise.all([
                registerFcmToken(registration),
                registerWebPushSubscription(registration),
            ]);

            return true;
        } catch (error) {
            debugLog('❌ ERROR: ' + error.message);
            return false;
        }
    }

    // ── Request permission + register (ONLY from Enable button) ──
    async function requestAndRegister() {
        try {
            debugLog('⏳ Requesting notification permission...');
            const permission = await Notification.requestPermission();
            debugLog('Permission result: ' + permission);
            if (permission !== 'granted') {
                debugLog('❌ Permission denied or dismissed');
                return false;
            }
            debugLog('✅ Permission granted');
            return await registerTokenOnly();
        } catch (error) {
            debugLog('❌ ERROR: ' + error.message);
            return false;
        }
    }

    // ── Public function for the "Enable Notifications" banner ──
    window.enablePushNotifications = async function () {
        const success = await requestAndRegister();
        if (success) {
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

    // ── Handle foreground messages (FCM) ──
    messaging.onMessage(function (payload) {
        console.log('[FCM] Foreground message:', payload);
    });

    // ── Startup ──
    function init() {
        // If permission was already granted, register tokens silently
        if (Notification.permission === 'granted') {
            debugLog('Permission already granted, registering...');
            registerTokenOnly();
            return;
        }

        // If user previously dismissed or enabled, don't show banner again
        if (localStorage.getItem('moc_push_enabled') === 'true' ||
            localStorage.getItem('moc_push_dismissed') === 'true') {
            return;
        }

        // Show the banner after a slight delay
        setTimeout(() => {
            const banner = document.getElementById('push-banner');
            if (banner) {
                banner.classList.remove('hidden');
                banner.classList.add('push-banner-show');
            }
        }, 2000);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
