# UniEmu Client Agent Guide

Этот документ описывает frontend-часть UniEmu Hub: как запускать проект, где лежит функционал, как он связан с backend API и какие соглашения соблюдать при изменениях.

## Запуск и проверка

- `npm run dev` запускает Vite dev server. По текущей конфигурации он слушает порт `8070`.
- `npm run build` собирает production-артефакты.
- `npm run lint` запускает ESLint.
- `npm run format` форматирует проект через Prettier.
- Фронтовые `.mjs` тесты должны лежать в отдельном каталоге `tests/`, а не рядом с исходниками в `src/`.
- Не полагайтесь на `tsc --noEmit` как обязательную проверку без предварительной диагностики: в текущей конфигурации он может зависать.
- Dev server проксирует `/api` на backend. По умолчанию target: `http://localhost:5083`; переопределение через `VITE_API_PROXY_TARGET`.
- Для production/static hosting API вызывается относительным путем `/api`, если `VITE_API_BASE_URL` не задан.

## Технологии

- React + Vite.
- TanStack Router для файловой маршрутизации.
- Zustand для глобального UI/cache состояния.
- Tailwind CSS через `@tailwindcss/vite`.
- Radix UI wrappers в `src/components/ui`.
- Monaco Editor для `.csx` скриптов.
- Recharts для графиков мониторинга.
- PWA через `vite-plugin-pwa`.

## Backend API интеграция

Ручной API-клиент находится в `src/api/uniemu-api.ts`.

Клиент:

- использует существующие TypeScript-модели из `src/types/uniemu.ts`;
- вызывает REST endpoints backend-а через `fetch`;
- бросает `ApiError` при non-2xx ответах;
- не зависит от NSwag или другой генерации;
- использует `VITE_API_BASE_URL`, если он задан, иначе относительные `/api/...` пути.

Zustand store в `src/store/uniemu-store.ts` является cache/UI слоем:

- `hydrate()` загружает emulators, events, scripts, CNC programs и tags с backend.
- `loadEmulatorDetails(emulatorId)` догружает карточку эмулятора, теги и telemetry.
- CRUD actions вызывают backend API и затем обновляют локальный cache ответом сервера.
- `packetRetention`, theme и другие UI-настройки остаются локальными.
- Старые seed-данные оставлены как reference, но не должны быть основным источником данных.

При добавлении новых API:

- сначала добавьте типы в `src/types/uniemu.ts`;
- затем добавьте функцию в `src/api/uniemu-api.ts`;
- затем подключите action в `src/store/uniemu-store.ts`;
- компоненты должны работать с store, а не напрямую размазывать `fetch` по UI.

## Основные страницы

### Dashboard: `src/routes/index.tsx`

Главная сводка:

- KPI по эмуляторам.
- Список активных/найденных эмуляторов.
- Быстрый старт/стоп через `toggleStatus`.
- Последние события из store.

### Emulators List: `src/routes/emulators/index.tsx`

Список эмуляторов:

- фильтр по имени;
- фильтр по статусу;
- карточки с target URL, тегами, uptime, total requests;
- создание эмулятора;
- быстрый старт/стоп;
- переход в карточку.

### Emulator Detail: `src/routes/emulators/$id.tsx`

Карточка эмулятора:

- вкладка Overview: конфигурация, интервалы, counters, редактирование через `EditEmulatorDrawer`;
- вкладка Tags: список тегов, inline preview для static bool/string, добавление и редактирование через `AddTagDrawer`;
- вкладка Monitoring: график telemetry, packet history, per-tag synthetic/derived series;
- вкладка Logs: события по выбранному эмулятору.

Особенность: telemetry обновляется периодически через `refreshTelemetry(id, 60)`.

### Scripts: `src/routes/scripts.tsx`

Хранилище `.csx`:

- дерево общих скриптов и скриптов по эмуляторам;
- создание скрипта;
- удаление;
- редактирование содержимого в Monaco;
- сохранение через backend `PATCH /api/scripts/{id}`.

Runtime пока не исполняет `.csx` безопасно; скрипты используются как сохраняемая конфигурация.

### CNC Storage: `src/routes/cnc.tsx`

Хранилище управляющих программ:

- общие CNC programs;
- CNC programs, привязанные к эмулятору;
- upload текстовых и бинарных файлов;
- просмотр/редактирование текстового G-code;
- download;
- удаление.

Runtime/Dispatcher использует CNC programs при ответах на `FileType=1` и `FileType=2`. Для полной совместимости с Dispatcher byte-safe хранение еще нужно доработать на backend.

### Settings: `src/routes/settings.tsx`

Локальные настройки frontend:

- `packetRetention` для отображения истории пакетов на странице мониторинга.

### Logs: `src/routes/logs.tsx`

Пока placeholder. Следующий шаг: подключить `/api/events` и сделать полноценную ленту системных событий.

## Компоненты

- `src/components/AddTagDrawer.tsx` отвечает за создание/редактирование тегов и их вложенных конфигов.
- `src/components/EditEmulatorDrawer.tsx` редактирует базовую конфигурацию эмулятора.
- `src/components/MonacoCsxEditor/` оборачивает Monaco для `.csx`.
- `src/components/tag-scenario/*` реализует UI и математику сценариев.
- `src/components/StatusBadge.tsx`, `TimeAgo.tsx`, `PagePlaceholder.tsx` - небольшие shared компоненты.
- `src/components/Layout/AppLayout.tsx` задает shell приложения, sidebar, theme toggle и первичный `hydrate()`.

## Модели домена

Источник frontend-типов: `src/types/uniemu.ts`.

Ключевые сущности:

- `Emulator` - станок/эмулятор, статус, target URL, interval, counters.
- `EmulatorTag` - параметр мониторинга, который отправляется в Dispatcher.
- `TagTrigger` - once/cron/interval.
- `TagCalcConfig` - формула генерации.
- `TagFormulaConfig` - связь с `.csx` или inline script.
- `TagScenarioConfig` - timeline-сценарий из сегментов.
- `ScriptFile` - `.csx` файл.
- `CncProgram` - управляющая программа.
- `TelemetryPoint` - точка графика.
- `SystemEvent` - событие backend/runtime.

Enum casing должен оставаться совместимым с backend:

- `EmulatorStatus`: `Running`, `Stopped`, `Error`, `Idle`.
- `TagType`: `int`, `double`, `string`, `bool`.
- `TagSource`: `static`, `formula`, `script`, `generator`, `cnc`, `scenario`.
- `ScriptScope`/`CncScope`: `shared`, `emulator`.
- `EventLevel`: `info`, `warn`, `error`, `success`.

## Соглашения разработки

- Не возвращайте фронт на in-memory seed как основной источник данных.
- Новые backend-backed операции должны проходить через `uniEmuApi` и store action.
- Не добавляйте NSwag-generated client без отдельного решения: текущий подход - ручной typed client.
- Не размазывайте доменную математику сценариев по страницам; используйте `src/components/tag-scenario/scenarioMath.ts`.
- Для UI компонентов сначала ищите готовые wrappers в `src/components/ui`.
- Для новых routes соблюдайте TanStack Router file-route pattern.
- При async actions используйте `void action()` в event handlers, если результат не нужен UI.
- Ошибки backend желательно выводить пользователю через store `apiError` или toast, а не только логировать.
- Не меняйте route tree вручную, если он генерируется tooling-ом.

## Известные ограничения

- `logs` page пока placeholder.
- `tsc --noEmit` может зависать.
- Полный frontend dependency graph имеет предупреждения/конфликты peer dependencies.
- Runtime пока не исполняет `.csx`.
- Dispatcher protocol fields (`ProtocolIntegrationId`, `UseInnerId`, `ProgramDirectory`) еще не вынесены в UI как полноценная конфигурация.
- Byte-safe хранение УП в backend еще требует отдельной доработки.
