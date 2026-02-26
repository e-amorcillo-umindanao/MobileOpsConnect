// ─────────────────────────────────────────────────────────
// Push Notification Initialization (Standard Web Push)
//
// Uses the standard Web Push API (PushManager.subscribe)
// which works on ALL browsers: Chrome, Firefox, Safari iOS.
//
// FCM is NOT used for push delivery — standard Web Push
// with our own VAPID keys handles everything.
//
// In-app toasts are handled separately by SignalR.
// ─────────────────────────────────────────────────────────
(function () {
    'use strict';

    if (!('Notification' in window) || !('serviceWorker' in navigator) || !('PushManager' in window)) {
        console.warn('[Push] This browser does not support push notifications.');
        return;
    }

    // ── Visible debug log (temporary — remove once push is confirmed working) ──
    function debugLog(msg) {
        console.log('[Push] ' + msg);
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

    // ── Register standard Web Push subscription ──
    async function registerSubscription() {
        try {
            debugLog('⏳ Waiting for service worker...');
            const registration = await navigator.serviceWorker.ready;
            debugLog('✅ SW ready: ' + registration.scope);

            // Get our VAPID public key from server
            debugLog('⏳ Getting VAPID key...');
            const vapidResponse = await fetch('/Notification/GetVapidKey');
            if (!vapidResponse.ok) {
                debugLog('❌ VAPID key fetch failed');
                return false;
            }
            const { publicKey } = await vapidResponse.json();
            if (!publicKey) {
                debugLog('❌ No VAPID key configured on server');
                return false;
            }
            debugLog('✅ VAPID key: ' + publicKey.substring(0, 20) + '...');

            // Check for existing subscription (might be from Firebase or old key)
            const existingSub = await registration.pushManager.getSubscription();
            if (existingSub) {
                debugLog('⚠️ Existing subscription found, unsubscribing...');
                await existingSub.unsubscribe();
                debugLog('✅ Old subscription removed');
            }

            // Subscribe with our own VAPID key
            debugLog('⏳ Creating Web Push subscription...');
            const subscription = await registration.pushManager.subscribe({
                userVisibleOnly: true,
                applicationServerKey: urlBase64ToUint8Array(publicKey),
            });
            debugLog('✅ Subscribed!');

            // Extract subscription details
            const subJson = subscription.toJSON();
            const payload = {
                endpoint: subJson.endpoint,
                p256dh: subJson.keys?.p256dh ?? '',
                auth: subJson.keys?.auth ?? '',
            };
            debugLog('📡 Endpoint: ' + payload.endpoint.substring(0, 50) + '...');

            // Send to server
            debugLog('⏳ Registering with server...');
            const response = await fetch('/Notification/RegisterSubscription', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload),
            });
            if (response.ok) {
                debugLog('✅ Push subscription registered!');
            } else {
                debugLog('❌ Server error: HTTP ' + response.status);
                return false;
            }

            return true;
        } catch (error) {
            debugLog('❌ ERROR: ' + error.message);
            return false;
        }
    }

    // ── Request permission + register (ONLY from Enable button with user gesture) ──
    async function requestAndRegister() {
        try {
            debugLog('⏳ Requesting notification permission...');
            const permission = await Notification.requestPermission();
            debugLog('Permission result: ' + permission);
            if (permission !== 'granted') {
                debugLog('❌ Permission denied');
                return false;
            }
            debugLog('✅ Permission granted');
            return await registerSubscription();
        } catch (error) {
            debugLog('❌ ERROR: ' + error.message);
            return false;
        }
    }

    // ── Public: Enable button handler ──
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

    // ── Public: Dismiss banner ──
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

    // ── Startup ──
    function init() {
        // If permission already granted, silently re-register (no permission request)
        if (Notification.permission === 'granted') {
            debugLog('Permission already granted, registering...');
            registerSubscription();
            return;
        }

        // Don't show banner if user already handled it
        if (localStorage.getItem('moc_push_enabled') === 'true' ||
            localStorage.getItem('moc_push_dismissed') === 'true') {
            return;
        }

        // Show banner after short delay
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
