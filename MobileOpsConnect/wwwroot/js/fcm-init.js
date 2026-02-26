// ─────────────────────────────────────────────────────────
// Push Notification Initialization (Standard Web Push)
//
// Uses the standard Web Push API (PushManager.subscribe)
// which works on ALL browsers: Chrome, Firefox, Safari iOS.
//
// In-app toasts are handled separately by SignalR.
// ─────────────────────────────────────────────────────────
(function () {
    'use strict';

    if (!('Notification' in window) || !('serviceWorker' in navigator) || !('PushManager' in window)) {
        console.warn('[Push] This browser does not support push notifications.');
        return;
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
            const registration = await navigator.serviceWorker.ready;

            // Get our VAPID public key from server
            const vapidResponse = await fetch('/Notification/GetVapidKey');
            if (!vapidResponse.ok) return false;
            const { publicKey } = await vapidResponse.json();
            if (!publicKey) return false;

            // Unsubscribe old subscription if it exists (e.g. from Firebase)
            const existingSub = await registration.pushManager.getSubscription();
            if (existingSub) await existingSub.unsubscribe();

            // Subscribe with our own VAPID key
            const subscription = await registration.pushManager.subscribe({
                userVisibleOnly: true,
                applicationServerKey: urlBase64ToUint8Array(publicKey),
            });

            // Send subscription to server
            const subJson = subscription.toJSON();
            const response = await fetch('/Notification/RegisterSubscription', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    endpoint: subJson.endpoint,
                    p256dh: subJson.keys?.p256dh ?? '',
                    auth: subJson.keys?.auth ?? '',
                }),
            });

            return response.ok;
        } catch (error) {
            console.error('[Push] Registration error:', error);
            return false;
        }
    }

    // ── Request permission + register (ONLY from Enable button) ──
    async function requestAndRegister() {
        const permission = await Notification.requestPermission();
        if (permission !== 'granted') return false;
        return await registerSubscription();
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
        if (Notification.permission === 'granted') {
            registerSubscription();
            return;
        }

        if (localStorage.getItem('moc_push_enabled') === 'true' ||
            localStorage.getItem('moc_push_dismissed') === 'true') {
            return;
        }

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
