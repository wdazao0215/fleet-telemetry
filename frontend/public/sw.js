/*
 * Service worker del panel del conductor.
 *
 * Su único objetivo es que la aplicación *arranque* sin red. Los datos de telemetría no se cachean
 * aquí: viven en IndexedDB, que es donde el propio código los gestiona con su cola y sus reintentos.
 * Mezclar ambas responsabilidades produciría dos copias de la verdad que se contradicen.
 */

const CACHE_NAME = "fleet-driver-v1";
const APP_SHELL = ["/driver", "/manifest.webmanifest", "/icon.svg"];

self.addEventListener("install", (event) => {
  event.waitUntil(
    caches
      .open(CACHE_NAME)
      .then((cache) => cache.addAll(APP_SHELL))
      // skipWaiting: sin esto, una versión nueva esperaría a que se cierren todas las pestañas
      // abiertas. En un móvil que nunca cierra la app, la actualización no llegaría jamás.
      .then(() => self.skipWaiting()),
  );
});

self.addEventListener("activate", (event) => {
  event.waitUntil(
    caches
      .keys()
      .then((names) =>
        Promise.all(names.filter((name) => name !== CACHE_NAME).map((name) => caches.delete(name))),
      )
      .then(() => self.clients.claim()),
  );
});

self.addEventListener("fetch", (event) => {
  const { request } = event;

  // Las peticiones a la API nunca se cachean ni se sirven desde caché: una posición vieja servida
  // como si fuera la respuesta del servidor sería peor que un error honesto.
  if (request.method !== "GET" || new URL(request.url).pathname.startsWith("/api/")) {
    return;
  }

  // Network-first para la navegación: si hay red, se sirve lo último; si no, el shell cacheado.
  // Cache-first daría una app desactualizada aunque la red estuviera perfecta.
  event.respondWith(
    fetch(request)
      .then((response) => {
        if (response.ok && request.url.startsWith(self.location.origin)) {
          const copy = response.clone();
          void caches.open(CACHE_NAME).then((cache) => cache.put(request, copy));
        }

        return response;
      })
      .catch(async () => {
        const cached = await caches.match(request);

        if (cached) {
          return cached;
        }

        // Una navegación sin red y sin coincidencia exacta cae al shell del panel: el conductor ve
        // su aplicación, no la página de error del navegador.
        return request.mode === "navigate"
          ? (await caches.match("/driver")) ?? Response.error()
          : Response.error();
      }),
  );
});
