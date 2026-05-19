/**
 * Global frontend feature flags.
 *
 * These are code-level toggles for hiding or enabling whole UI blocks.
 */

/** Show the system events feed on dashboard and related screens. */
export const SHOW_EVENTS_FEED = false;

/** Temporarily show scenario tag value preview charts on emulator details. */
export const SHOW_TAG_SCENARIO_PREVIEWS = false;

/** Enable Zustand store persistence in localStorage. */
export const PERSIST_STORE: boolean =
  (typeof import.meta !== 'undefined' &&
    (import.meta as { env?: Record<string, string | undefined> }).env?.VITE_PERSIST_STORE === 'true') ||
  (typeof window !== 'undefined' &&
    (window as unknown as { __UNIEMU_PERSIST__?: boolean }).__UNIEMU_PERSIST__ === true);
