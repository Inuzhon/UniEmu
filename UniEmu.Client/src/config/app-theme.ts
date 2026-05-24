export type AppTheme = 'light' | 'dark';

export const appThemeStorageKey = 'app-theme';
export const appSidebarCollapsedStorageKey = 'app-sidebar-collapsed';

export const appThemeConfig = {
  defaultTheme: 'light' as AppTheme,
  pwa: {
    light: {
      themeColor: '#f8fafc',
      backgroundColor: '#f8fafc',
    },
    dark: {
      themeColor: '#0b1220',
      backgroundColor: '#0b1220',
    },
  },
};

function isAppTheme(value: string | null): value is AppTheme {
  return value === 'light' || value === 'dark';
}

function readLocalStorageItem(key: string): string | null {
  if (typeof localStorage === 'undefined') {
    return null;
  }

  try {
    return localStorage.getItem(key);
  } catch {
    return null;
  }
}

function writeLocalStorageItem(key: string, value: string) {
  if (typeof localStorage === 'undefined') {
    return;
  }

  try {
    localStorage.setItem(key, value);
  } catch {
    // Ignore unavailable storage, for example in restricted browser contexts.
  }
}

export function getInitialAppTheme(): AppTheme {
  const savedTheme = readLocalStorageItem(appThemeStorageKey);
  return isAppTheme(savedTheme) ? savedTheme : appThemeConfig.defaultTheme;
}

export function saveAppThemePreference(theme: AppTheme) {
  writeLocalStorageItem(appThemeStorageKey, theme);
}

export function getInitialSidebarCollapsed(): boolean {
  const savedCollapsed = readLocalStorageItem(appSidebarCollapsedStorageKey);
  return savedCollapsed === 'true';
}

export function saveSidebarCollapsedPreference(collapsed: boolean) {
  writeLocalStorageItem(appSidebarCollapsedStorageKey, String(collapsed));
}

export function applyDocumentAppTheme(theme: AppTheme, documentRef: Document = document) {
  const htmlElement = documentRef.documentElement;
  htmlElement.classList.toggle('light', theme === 'light');

  const themeColorMeta = documentRef.head.querySelector('meta[name="theme-color"]');
  themeColorMeta?.setAttribute('content', appThemeConfig.pwa[theme].themeColor);
}
