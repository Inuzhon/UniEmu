---
tags:
  - uniemu
  - frontend
  - ui
---

# Frontend-консоль

`UniEmu.Client` - веб-консоль на React/Vite. Она предназначена для ежедневной работы с эмуляторами, тегами, скриптами, CNC-программами и мониторингом.

## Технологии

- React 19;
- Vite;
- TanStack Router;
- Zustand;
- Tailwind CSS;
- Radix UI wrappers;
- Monaco Editor;
- Recharts;
- SignalR client;
- PWA через `vite-plugin-pwa`.

## Состояние приложения

Главный store находится в `src/store/uniemu-store.ts`.

Он хранит:

- `emulators`;
- `events`;
- `tagsByEmulator`;
- `scripts`;
- `cncPrograms`;
- `telemetryByEmulator`;
- `online`;
- `loading`;
- `apiError`;
- `packetRetention`.

`hydrate()` загружает эмуляторы, события, скрипты, CNC-программы и теги по каждому эмулятору. CRUD actions всегда идут через backend API и после ответа обновляют локальный cache.

Persist по умолчанию отключен. Если `VITE_PERSIST_STORE=true`, сохраняется только `packetRetention`.

## API client

Frontend использует ручной typed fetch client `src/api/uniemu-api.ts`.

Если задан `VITE_API_BASE_URL`, запросы идут на этот origin. Если нет, используются относительные пути `/api/...`, что удобно для production/static hosting за тем же backend.

Non-2xx ответы превращаются в `ApiError`.

## Страницы

### Dashboard `/`

Показывает:

- KPI по эмуляторам;
- readiness и статусы;
- поиск;
- карточки эмуляторов;
- быстрый start/stop.

Для сводки дашборда ошибочным считается не только эмулятор со `Status = Error`, но и любой эмулятор с заполненным `LastError`. Поэтому runtime-сбои, при которых управляющий статус остается `Running` (например timeout отправки, ошибка расчета тега, ошибка cron/once-тега), уменьшают счетчик запущенных, увеличивают счетчик ошибок, окрашивают карточку в ошибочный статус и поднимают ее в сортировке.

### Emulators `/emulators`

Список эмуляторов:

- фильтр по имени;
- фильтр по статусу;
- создание эмулятора;
- переход в карточку;
- start/stop.

### Emulator detail `/emulators/$id`

Карточка эмулятора:

- overview;
- tags;
- monitoring;
- logs по эмулятору;
- SignalR-подписка на конкретный эмулятор;
- скачивание Dispatcher XML template;
- удаление.

Во вкладке Tags drawer создания/редактирования тега учитывает специальные параметры Universal-протокола. Для static string-тегов с `SpecialParameter = PrgName` или `Subprogram` поле статического значения заменяется на searchable dropdown CNC-программ. В нем показываются только видимые для текущего эмулятора программы:

- `Общие УП` - shared CNC-программы;
- `УП этого эмулятора` - программы с `scope = emulator` и текущим `emulatorId`.

При выборе в тег записывается имя файла программы, то есть `Preview` остается обычной строкой. Если у существующего тега сохранено имя, которого уже нет в списке, UI показывает его как текущее значение, чтобы старую конфигурацию можно было открыть и сохранить без потери данных.

### CNC `/cnc`

Хранилище управляющих программ:

- shared/emulator scope;
- drag and drop upload;
- просмотр;
- редактирование текстовых файлов;
- download;
- удаление.

Binary upload сейчас хранит placeholder content вида `[binary: name]`, редактирование binary заблокировано.

### Scripts `/scripts`

CSX-хранилище:

- дерево shared/emulator скриптов;
- создание;
- rename/delete;
- Monaco editor;
- dirty-state;
- backend IntelliSense.

Подробно о возможностях редактора, подсветке, автодополнении, snippets и API скриптов: [[16 Редактор скриптов и API]].

### Logs и Settings

Маршруты есть, но скрыты из sidebar. Logs пока placeholder. Settings управляет только `packetRetention`.

## Realtime в UI

SignalR-клиент подключается к `/hubs/runtime-updates`.

События:

- `TelemetryPoint`;
- `TagValue`;
- `EmulatorUpdated`;
- `EventCreated`.

При старте UI вызывает `SubscribeAll`. Карточка эмулятора дополнительно подписывается на `SubscribeEmulator(id)` и отписывается при уходе.

## PWA

Manifest лежит в `public/manifest.webmanifest`. Service Worker регистрируется вручную через `src/pwa-register.ts`.

Регистрация отключается:

- в iframe;
- на preview-хостах;
- в небезопасном контексте, кроме localhost.

Workbox исключает `/api`, чтобы API не попадал под offline fallback.

Подробно про установку приложения, service worker, auto update, cache-стратегии и границы offline-режима: [[17 PWA и офлайн-режим]].

## Сборка

```powershell
yarn dev
yarn build
yarn lint
yarn format
```

Dev server слушает порт `8070` и проксирует backend.

> [!NOTE]
> В `package.json` указан Yarn 1, но frontend Dockerfile использует `npm install`. Это стоит учитывать при унификации CI/CD и контейнерной сборки.
