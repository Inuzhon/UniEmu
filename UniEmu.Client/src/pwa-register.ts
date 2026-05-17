export async function registerPwa() {
  if (typeof window === 'undefined' || !('serviceWorker' in navigator)) return;

  const inIframe = (() => {
    try {
      return window.self !== window.top;
    } catch {
      return true;
    }
  })();

  const host = window.location.hostname;
  const isPreviewHost =
    host.includes('id-preview--') ||
    host.includes('lovableproject.com') ||
    host.includes('lovableproject-dev.com') ||
    host.startsWith('preview--');

  if (inIframe || isPreviewHost || !window.isSecureContext) {
    try {
      const regs = await navigator.serviceWorker.getRegistrations();
      await Promise.all(regs.map((registration) => registration.unregister()));
      if ('caches' in window) {
        const keys = await caches.keys();
        await Promise.all(keys.map((key) => caches.delete(key)));
      }
    } catch {
      /* noop */
    }
    return;
  }

  try {
    const baseUrl = import.meta.env.BASE_URL || '/';
    await navigator.serviceWorker.register(`${baseUrl}sw.js`, { scope: baseUrl });
  } catch (err) {
    console.warn('[pwa] SW registration failed:', err);
  }
}
