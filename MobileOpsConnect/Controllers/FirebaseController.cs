using Microsoft.AspNetCore.Mvc;

namespace MobileOpsConnect.Controllers;

/// <summary>
/// Serves the Firebase Messaging service worker with config injected from appsettings.json
/// so that sensitive keys are never hardcoded in static JS files.
/// </summary>
public class FirebaseController : Controller
{
    private readonly IConfiguration _config;

    public FirebaseController(IConfiguration config)
    {
        _config = config;
    }

    /// <summary>
    /// GET /firebase-messaging-sw.js — Dynamically generated service worker
    /// </summary>
    [HttpGet("/firebase-messaging-sw.js")]
    [ResponseCache(Duration = 3600)]
    public IActionResult ServiceWorker()
    {
        var c = _config.GetSection("Firebase:ClientConfig");

        var js = $@"// Firebase Messaging Service Worker (dynamically served)
// This file MUST be served from the root scope (/)

importScripts('https://www.gstatic.com/firebasejs/10.12.0/firebase-app-compat.js');
importScripts('https://www.gstatic.com/firebasejs/10.12.0/firebase-messaging-compat.js');

firebase.initializeApp({{
    apiKey: ""{c["ApiKey"]}"",
    authDomain: ""{c["AuthDomain"]}"",
    projectId: ""{c["ProjectId"]}"",
    storageBucket: ""{c["StorageBucket"]}"",
    messagingSenderId: ""{c["MessagingSenderId"]}"",
    appId: ""{c["AppId"]}""
}});

const messaging = firebase.messaging();

// Handle background messages (when the browser tab is not focused)
messaging.onBackgroundMessage(function (payload) {{
    console.log('[SW] Background message received:', payload);

    const notificationTitle = payload.notification?.title || 'MobileOpsConnect';
    const notificationOptions = {{
        body: payload.notification?.body || '',
        icon: '/favicon.ico',
        badge: '/favicon.ico',
        data: payload.data,
        tag: 'moc-notification',
    }};

    self.registration.showNotification(notificationTitle, notificationOptions);
}});

// Handle notification click — navigate to the app
self.addEventListener('notificationclick', function (event) {{
    event.notification.close();

    const url = event.notification.data?.url || '/';

    event.waitUntil(
        clients.matchAll({{ type: 'window', includeUncontrolled: true }}).then(function (clientList) {{
            for (const client of clientList) {{
                if (client.url.includes(self.location.origin) && 'focus' in client) {{
                    return client.focus();
                }}
            }}
            return clients.openWindow(url);
        }})
    );
}});
";

        return Content(js, "application/javascript");
    }
}
