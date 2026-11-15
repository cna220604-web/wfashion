const CACHE_NAME = 'w-studio-shell-v1';
const APP_SHELL = [
    '/offline.html',
    '/manifest.webmanifest',
    '/css/site.css',
    '/css/editorial.css',
    '/js/site.js',
    '/js/pwa.js',
    '/js/storefront.js',
    '/lib/bootstrap/dist/css/bootstrap.min.css',
    '/lib/bootstrap/dist/js/bootstrap.bundle.min.js',
    '/lib/jquery/dist/jquery.min.js',
    '/images/pwa/icon-192.png',
    '/images/pwa/icon-512.png',
    '/images/pwa/icon-maskable-512.png',
    '/images/pwa/apple-touch-icon.png'
];
const STATIC_PATH = /^\/(?:css|js|lib|images\/products|images\/pwa)\//;

self.addEventListener('install', event => {
    event.waitUntil(caches.open(CACHE_NAME).then(cache => cache.addAll(APP_SHELL)));
    self.skipWaiting();
});

self.addEventListener('activate', event => {
    event.waitUntil(caches.keys().then(keys => Promise.all(
        keys.filter(key => key.startsWith('w-studio-shell-') && key !== CACHE_NAME)
            .map(key => caches.delete(key))
    )));
    self.clients.claim();
});

self.addEventListener('fetch', event => {
    const request = event.request;
    const url = new URL(request.url);
    if (request.method !== 'GET' || url.origin !== self.location.origin) return;

    if (request.mode === 'navigate') {
        event.respondWith(fetch(request).catch(async () =>
            (await caches.match('/offline.html')) || Response.error()
        ));
        return;
    }

    if (!STATIC_PATH.test(url.pathname)) return;

    event.respondWith(fetch(request).then(async response => {
        if (response.ok && response.type === 'basic') {
            await caches.open(CACHE_NAME).then(cache => cache.put(request, response.clone()));
        }
        return response;
    }).catch(async () =>
        (await caches.match(request, { ignoreSearch: true })) || Response.error()
    ));
});
