// FinTrack's service worker. It only shows push notifications and opens FinTrack when one is tapped. There's no
// fetch handler, so it never caches or serves anything and pages always come from the server. Registered from the
// Push tab when someone turns push on, see ftPushSubscribe in js/app.js.

self.addEventListener("install", () => self.skipWaiting());

self.addEventListener("activate", event => event.waitUntil(self.clients.claim()));

// The message is JSON from the server. A title, a body and a path in the app, see PushMessage
self.addEventListener("push", event => {
    let message = {title: "FinTrack", body: ""};
    try {
        message = event.data?.json() ?? message;
    } catch {
        // Not JSON, shown with the plain title
    }

    const url = new URL(message.path ?? "", self.registration.scope).href;
    event.waitUntil(self.registration.showNotification(message.title, {
        body: message.body,
        icon: "icon-192.png",
        badge: "badge-96.png",
        data: {url}
    }));
});

// Tapping one goes to its page. In an open FinTrack window, which navigates itself (see js/app.js), or a new one
self.addEventListener("notificationclick", event => {
    event.notification.close();
    event.waitUntil(openApp(event.notification.data?.url ?? self.registration.scope));
});

async function openApp(url) {
    const windows = await self.clients.matchAll({type: "window", includeUncontrolled: true});
    const app = windows.find(x => x.url.startsWith(self.registration.scope));
    if (app) {
        app.postMessage({type: "ft-open", url});
        await app.focus();
        return;
    }

    await self.clients.openWindow(url);
}
