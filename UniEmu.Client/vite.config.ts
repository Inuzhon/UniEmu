import { defineConfig, loadEnv, mergeConfig, type PluginOption, type UserConfig } from "vite";
import path from "node:path";
import tailwindcss from "@tailwindcss/vite";
import tsConfigPaths from "vite-tsconfig-paths";
import viteReact from "@vitejs/plugin-react";
import { tanstackRouter } from "@tanstack/router-plugin/vite";
import { VitePWA } from "vite-plugin-pwa";
import { appThemeConfig } from "./src/config/app-theme";

const vendorChunkGroups = [
  {
    name: "vendor-monaco-vscode",
    test: (id: string) => matchesNodeModule(id, [
      "monaco-languageclient",
      "vscode-jsonrpc",
      "vscode-languageclient",
      "vscode-ws-jsonrpc",
      "@codingame",
      "@vscode",
      "vscode",
    ]),
    priority: 90,
  },
  {
    name: "vendor-monaco",
    test: (id: string) => matchesNodeModule(id, [
      "@monaco-editor/react",
      "monaco-editor",
    ]),
    priority: 80,
  },
  {
    name: "vendor-tanstack",
    test: (id: string) => matchesNodeModule(id, [
      "@tanstack",
    ]),
    priority: 60,
  },
  {
    name: "vendor-radix",
    test: (id: string) => matchesNodeModule(id, [
      "@radix-ui",
    ]),
    priority: 50,
  },
  {
    name: "vendor-charts",
    test: (id: string) => matchesNodeModule(id, [
      "recharts",
      "d3-",
      "internmap",
      "victory-vendor",
    ]),
    priority: 40,
  },
  {
    name: "vendor-ui-utils",
    test: (id: string) => matchesNodeModule(id, [
      "class-variance-authority",
      "clsx",
      "cmdk",
      "date-fns",
      "embla-carousel-react",
      "input-otp",
      "lucide-react",
      "react-day-picker",
      "react-hook-form",
      "@hookform",
      "react-markdown",
      "sonner",
      "tailwind-merge",
      "vaul",
      "zod",
      "zustand",
    ]),
    priority: 30,
  },
  {
    name: "vendor-misc",
    test: /node_modules[\\/]/,
    priority: 0,
  },
];

function matchesNodeModule(id: string, packageNames: string[]) {
  const normalizedId = id.replace(/\\/g, "/");
  if (!normalizedId.includes("/node_modules/")) {
    return false;
  }

  return packageNames.some((packageName) => normalizedId.includes(`/node_modules/${packageName}`));
}

export default defineConfig(async (env) => {
  const { command, mode } = env;

  const plugins: PluginOption[] = [
    tailwindcss(),
    tsConfigPaths({ projects: ["./tsconfig.json"] }),
    tanstackRouter({
      routeFileIgnorePattern: "components",
    }),
    viteReact(),
    // PWA: устанавливаемое приложение + офлайн через Service Worker.
    // Внимание: SW работает только по HTTPS (или localhost). На обычном
    // HTTP-домене браузер просто проигнорирует регистрацию — manifest
    // продолжит отдаваться, остальное активируется как только появится TLS.
    VitePWA({
      registerType: "autoUpdate",
      injectRegister: null, // регистрируем вручную в src/main с защитой от iframe
      devOptions: { enabled: false },
      manifest: {
        name: "UniEmu Web Console",
        short_name: "UniEmu",
        description: "Панель управления эмуляторами протокола Universal.",
        start_url: "/",
        scope: "/",
        display: "standalone",
        orientation: "any",
        background_color: appThemeConfig.pwa.light.backgroundColor,
        theme_color: appThemeConfig.pwa.light.themeColor,
        icons: [
          {
            src: "/icons/icon-192.png",
            sizes: "192x192",
            type: "image/png",
            purpose: "any maskable",
          },
          {
            src: "/icons/icon-512.png",
            sizes: "512x512",
            type: "image/png",
            purpose: "any maskable",
          },
        ],
      },
      workbox: {
        maximumFileSizeToCacheInBytes: 12 * 1024 * 1024,
        globPatterns: ["**/*.{js,css,html,ico,png,svg,webmanifest,woff2}"],
        navigateFallback: "/index.html",
        navigateFallbackDenylist: [/^\/api\//, /^\/~oauth/],
        cleanupOutdatedCaches: true,
        runtimeCaching: [
          {
            urlPattern: ({ request }) => request.mode === "navigate",
            handler: "NetworkFirst",
            options: { cacheName: "html", networkTimeoutSeconds: 3 },
          },
          {
            urlPattern: ({ request }) =>
              ["style", "script", "worker", "image", "font"].includes(request.destination),
            handler: "StaleWhileRevalidate",
            options: { cacheName: "assets" },
          },
        ],
      },
    }),
  ];

  // Инжект VITE_* переменных в import.meta.env
  const define: Record<string, string> = {};
  const loadedEnv = loadEnv(mode, process.cwd(), "VITE_");
  for (const [key, value] of Object.entries(loadedEnv)) {
    define[`import.meta.env.${key}`] = JSON.stringify(value);
  }

  const config: UserConfig = {
    base: "/",
    define,
    resolve: {
      alias: {
        "@": path.resolve(process.cwd(), "src"),
      },
      dedupe: [
        "react",
        "react-dom",
        "react/jsx-runtime",
        "react/jsx-dev-runtime",
        "@tanstack/react-query",
        "@tanstack/query-core",
      ],
      tsconfigPaths: true,
    },
    plugins,
    build: {
      rolldownOptions: {
        output: {
          strictExecutionOrder: true,
          codeSplitting: {
            includeDependenciesRecursively: false,
            groups: vendorChunkGroups,
          },
        },
      },
    },
  };

  return mergeConfig({
    server: {
      host: "::",
      port: 8070,
      allowedHosts: ["localhost", "127.0.0.1", "uniemu.xarleyn.me"],
      proxy: {
        "/api": {
          target: loadedEnv.VITE_API_PROXY_TARGET ?? "http://localhost:5083",
          changeOrigin: true,
        },
        "/hubs": {
          target: loadedEnv.VITE_API_PROXY_TARGET ?? "http://localhost:5083",
          changeOrigin: true,
          ws: true,
        },
      },
    }
  }, config);
});
