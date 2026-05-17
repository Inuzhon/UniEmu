export type AppTheme = 'light' | 'dark';

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

export function getInitialAppTheme(): AppTheme {
  if (typeof localStorage === 'undefined') {
    return appThemeConfig.defaultTheme;
  }

  const savedTheme = localStorage.getItem('app-theme');
  return isAppTheme(savedTheme) ? savedTheme : appThemeConfig.defaultTheme;
}

export function applyDocumentAppTheme(theme: AppTheme, documentRef: Document = document) {
  const htmlElement = documentRef.documentElement;
  htmlElement.classList.toggle('light', theme === 'light');

  const themeColorMeta = documentRef.head.querySelector('meta[name="theme-color"]');
  themeColorMeta?.setAttribute('content', appThemeConfig.pwa[theme].themeColor);
}
