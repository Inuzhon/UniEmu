# UniEmu Web Console

Веб-консоль для управления парком эмуляторов промышленного оборудования (станки с ЧПУ, прессы, роботы, конвейеры), которые публикуют телеметрию в SCADA-системы по протоколу UniEmu. Приложение позволяет конфигурировать эмуляторы, описывать их теги (формулы, генераторы, сценарии), хранить C#-скрипты и управляющие программы (G-код), наблюдать за их работой в реальном времени.

- **Стек:** TanStack Start v1 (React 19, Vite 7), Tailwind v4, shadcn/ui, Zustand (с `persist` в `localStorage`), Recharts, Monaco Editor, lucide-react.
- **Тема:** тёмная индустриальная, без градиентов на основном фоне, семантические токены в `src/styles.css`.
- **Локализация:** русский интерфейс.
- **Развёртывание:** Cloudflare Workers (edge SSR). PWA-манифест и Service Worker подключаются только в защищённом контексте (HTTPS / localhost), Monaco полностью встроен в бандл (офлайн).
- **Текущая версия:** см. `src/data/changelog.ts` (`APP_VERSION`).

---

## 1. Назначение

UniEmu — это эмулятор-агент: он притворяется реальным станком/устройством и периодически отправляет на SCADA-эндпоинт пакеты со значениями тегов. Веб-консоль — управляющая надстройка над парком таких агентов:

- держит реестр эмуляторов и их статус (`Running / Stopped / Error / Idle`);
- описывает «что именно отправлять» — теги с типами (`int / double / string / bool`), ключами (включая каталог специализированных параметров протокола UniEmu) и формулами;
- даёт общее хранилище C#-скриптов (`.csx`) и управляющих программ ЧПУ (G-код), привязанных либо ко всему парку (`shared`), либо к конкретному эмулятору;
- визуализирует телеметрию (мониторинг, спарклайны, сценарные диаграммы);
- ведёт журнал событий и хранит N последних отправленных пакетов (настраивается).

Все данные пока живут в браузере (Zustand + `localStorage`), бэкенд не подключён — это «фронтовый макет/консоль», готовый к интеграции с реальными агентами.

---

## 2. Архитектура

```
src/
├── routes/                       # File-based routing (TanStack Router)
│   ├── __root.tsx                # html/head/body shell, PWA-регистрация, Layout
│   ├── index.tsx                 # Главная (дашборд)
│   ├── emulators.index.tsx       # Список эмуляторов
│   ├── emulators.$id.tsx         # Карточка эмулятора (теги + мониторинг)
│   ├── scripts.tsx               # IDE для .csx-скриптов
│   ├── cnc.tsx                   # Хранилище управляющих программ
│   ├── logs.tsx                  # Журнал (placeholder)
│   └── settings.tsx              # Глобальные настройки
├── components/
│   ├── Layout/AppLayout.tsx      # Сворачиваемый sidebar + футер с версией
│   ├── AddTagDrawer.tsx          # Создание/редактирование тега
│   ├── EditEmulatorDrawer.tsx    # Редактирование эмулятора
│   ├── MonacoCsxEditor/          # Локальный Monaco с темой uniemu-dark
│   ├── ChangelogDialog.tsx       # Диалог с историей версий
│   ├── StatusBadge.tsx, TimeAgo.tsx, PagePlaceholder.tsx
│   ├── tag-scenario/             # Сценарии (timeline) для тегов
│   │   ├── CalcConfigFields.tsx
│   │   ├── ScenarioEditor.tsx
│   │   ├── ScenarioPreviewChart.tsx
│   │   └── scenarioMath.ts       # sampleScenario / valueAt / formatDuration
│   └── ui/                       # shadcn-компоненты
├── store/uniemu-store.ts         # Zustand: эмуляторы, теги, телеметрия, скрипты, УП, события
├── types/uniemu.ts               # Типы доменной модели
├── data/changelog.ts             # APP_VERSION + журнал релизов
├── pwa-register.ts               # Безопасная регистрация SW (skip в iframe/HTTP)
├── styles.css                    # Tailwind v4 + дизайн-токены (oklch)
└── utils/format.ts               # formatNumber / formatUptime
public/
├── manifest.webmanifest
└── icons/                        # PWA-иконки (192/512 + apple-touch)
```

---

## 3. Доменная модель (`src/types/uniemu.ts`)

### 3.1. Эмулятор

```ts
interface Emulator {
  id: string;
  name: string;
  status: "Running" | "Stopped" | "Error" | "Idle";
  targetUrl: string;          // SCADA-эндпоинт
  intervalSec: number;        // период отправки пакета
  lastRun / nextRun: ISO | null;
  lastError: string | null;
  tagsCount: number;
  uptimeSec: number;
  totalRequests: number;
}
```

### 3.2. Тег

Тег — единица данных, отправляемая в пакете. Ключевые поля:

- `name` — человекочитаемое имя.
- `key: TagKey` — произвольная строка-ключ (есть каталог известных ключей в `AddTagDrawer`).
- `type` — `int | double | string | bool`.
- `source` — источник значения:
  - `static` — фиксированное значение;
  - `formula` — inline или ссылка на `.csx`-скрипт;
  - `script` — выделенный `.csx`-скрипт;
  - `generator` — параметрическая формула (`TagCalcConfig`);
  - `cnc` — значение из контекста выполнения УП;
  - `scenario` — таймлайн-сценарий (см. ниже).
- `trigger: TagTrigger` — режим срабатывания:
  - `once` — один раз при `onStart` или `onStop`;
  - `interval` — каждые N `ms / sec / min`.
- `calc?: TagCalcConfig` — параметры формулы (для `generator / formula`).
- `formula?: TagFormulaConfig` — `scriptId` или `inlineScript`.
- `scenario?: TagScenarioConfig` — таймлайн.
- `specialParameter?: SpecialParameter` — отдельное обособленное поле, **не подставляется в name/key**. Каталог: `None, PrgName, PartCounter, ErrorNum, FeedOvr, SpindleOvr, JogOvr, FrameNum, FrameText, ToolNum, WorkMode, SystemState, MachineReadiness, TechnologicalStop, EmergencyStop, FeedRate, ErrorText, CycleTime, SpindleSpeed, SpindleLoad, AxisLoad, AxisPosition, Message, CNCModel, FirmwareVersion, SerialNumber, PLCVersion, Subprogram`.

### 3.3. Формула расчёта (`TagCalcConfig`)

`type: CalcType` — одно из:

| Тип             | Описание |
|-----------------|----------|
| `None`          | Нет расчёта |
| `Text`          | Статический текст |
| `Line`          | Линейная интерполяция `start → finish` за `duration` |
| `Curve`         | Степенная кривая с показателем `curvature` |
| `SquircleEarly` / `SquircleLate` | Сглаженные ease-кривые |
| `Random`        | Случайное в `[start, finish]` (детерминированно по фазе) |
| `Sinusoid`      | `start + amplitude·sin(2πt/period)` |
| `Square`        | Меандр амплитуды `amplitude` с периодом `period` |
| `Sawtooth`      | Пила |

Дополнительно: `distortion` (0..100%) — добавляет шум, пропорциональный масштабу значения.

### 3.4. Сценарий тега (`TagScenarioConfig`)

Упорядоченный список сегментов, каждый со своей формулой и длительностью в секундах:

```ts
interface TagScenarioSegment { id; duration; calc: TagCalcConfig; label?; }
interface TagScenarioConfig {
  segments: TagScenarioSegment[];
  continueOnFormulaEnd: "NoSignal" | "Zero" | "Repeat" | "Stretch";
  startValue?: string;
}
```

Поведение по достижению конца таймлайна:
- `NoSignal` — публикация прекращается (`null`);
- `Zero` — удерживается `0`;
- `Repeat` — сценарий зацикливается;
- `Stretch` — удерживается последнее значение.

Чистая математика вынесена в `src/components/tag-scenario/scenarioMath.ts`:
- `sampleScenario(scenario, totalSamples)` — массив точек для графиков;
- `valueAt(scenario, tSec)` — значение в момент времени;
- `totalDuration / formatDuration / defaultSegment`.

### 3.5. Скрипты, УП, телеметрия, события

- `ScriptFile` — `.csx`, поле `scope: "shared" | "emulator"` + `emulatorId?`.
- `CncProgram` — G-код, аналогично; флаг `isBinary` блокирует редактирование нетекстовых файлов.
- `TelemetryPoint` — `{ timestamp, values: Record<tagId, number> }`, хранится по эмулятору.
- `SystemEvent` — `{ level: "info"|"warn"|"error"|"success", message, emulatorId, timestamp }`.

---

## 4. Стор (`src/store/uniemu-store.ts`)

Zustand + `persist` в `localStorage` (ключ хранилища). Сидируется демо-данными: 6 эмуляторов, набор тегов (включая `Feed` со зацикленным сценарием Line→Sinusoid→Line), несколько `.csx`-скриптов и G-программ.

Основные actions:

- **Эмуляторы:** `createEmulator`, `updateEmulator`, `deleteEmulator`, `toggleStatus`, `restartEmulator`.
- **Теги:** `addTag`, `updateTag`, `deleteTag` (принимают полный payload, включая `scenario` и `specialParameter`).
- **Скрипты:** `createScript`, `updateScript`, `deleteScript`.
- **УП:** `createCncProgram`, `updateCncProgram`, `deleteCncProgram`.
- **Телеметрия:** автогенерация `tagSeries` (для сценариев — через `sampleScenario`), ограничение длины через `packetRetention`.
- **Настройки:** `setPacketRetention`.

---

## 5. Маршруты и страницы

### 5.1. `/` — Главная (`src/routes/index.tsx`)

Дашборд:
- KPI-карточки (всего эмуляторов, Running, Error, суммарно тегов и запросов);
- поиск + список последних эмуляторов с быстрыми действиями (Start/Stop, переход в карточку);
- секция активности (последние `SystemEvent`) с цветовой индикацией уровня.
- Логотип «U» + название продукта (без вспомогательной строки версии — она в футере).

### 5.2. `/emulators` — Список (`emulators.index.tsx`)

- Поиск + фильтр по статусу (`all / Running / Stopped / Error`);
- таблица с именем, статусом, URL, интервалом, временем последнего запуска, числом тегов и пакетов;
- кнопка `+ Создать` — диалог с именем, `targetUrl` (по умолчанию `https://scada.local/api/ingest`) и `intervalSec`.

### 5.3. `/emulators/$id` — Карточка эмулятора (`emulators.$id.tsx`)

Двухпанельный экран с разделами в `<Collapsible>`:

1. **Шапка:** имя, статус, кнопки `Start/Stop`, `Перезапуск`, `Редактировать` (открывает `EditEmulatorDrawer`), `Удалить`.
2. **Параметры:** `targetUrl`, `intervalSec`, `lastRun`, `nextRun`, `uptime`, `totalRequests`, `lastError`.
3. **Теги:** таблица с колонками Имя / Key / Тип / Источник / Calc / Триггер / Предпросмотр.
   - Для `source === "scenario"` в Calc: `сценарий · N сегм · Σ MM:SS` (+ значок повтора при `Repeat`), в Триггере: `по таймлайну`, в Предпросмотре — мини-спарклайн (`ScenarioSparkline`).
   - Кнопки `+ Добавить тег` / `Pencil` / `Trash2` — открывают `AddTagDrawer`.
4. **Мониторинг:**
   - линейный график телеметрии (Recharts);
   - таблица текущих значений тегов;
   - блок «Сценарии тегов» — по карточке `ScenarioPreviewChart` на каждый scenario-тег с курсором текущей позиции.

### 5.4. `/scripts` — Скрипты (`scripts.tsx`)

IDE-подобный экран:
- слева — дерево файлов с группировкой `Общие (shared/)` и по эмуляторам, поиск;
- справа — `MonacoCsxEditor` с темой `uniemu-dark`, IntelliSense по UniEmu Host API (`Tag()`, `SetTag()`, `Log()`, `Sleep()`), signature help;
- toolbar: `+ Новый`, `Сохранить` (с dirty-state), `Удалить`, переключатель `shared / emulator`.

### 5.5. `/cnc` — Хранилище УП (`cnc.tsx`)

- drag&drop загрузка файлов G-кода (расширения `.nc/.gcode/.g/.tap/.cnc/.mpf/.spf/.ngc/.eia/.txt/.prg/.min/.pim/.sub`);
- группировка `shared` / по эмуляторам, поиск, размер, дата;
- редактор `Textarea` для текстовых файлов; для бинарных — только просмотр свойств и `Скачать`.

### 5.6. `/logs` — Журнал

Сейчас `PagePlaceholder` («Журналы бэкенда и системные события»). Зарезервировано под полноценный просмотрщик.

### 5.7. `/settings` — Настройки

Глобальные параметры приложения. На текущий момент:
- **Хранение пакетов телеметрии** — слайдер `packetRetention` (число последних пакетов на эмулятор).

---

## 6. Layout, навигация, версия

`src/components/Layout/AppLayout.tsx`:
- сворачиваемый sidebar (иконки + тултипы в свёрнутом состоянии);
- пункты: Главная, Эмуляторы, Скрипты, Хранилище УП, Логи, Настройки;
- верхняя шапка с названием раздела и кнопками `Перезагрузить / Настройки` временно скрыта;
- футер с версией → клик открывает `ChangelogDialog` (markdown-история из `src/data/changelog.ts`).

---

## 7. PWA и офлайн

- `public/manifest.webmanifest` + иконки 192/512 и `apple-touch-icon`.
- `vite-plugin-pwa` с `autoUpdate`, кэширование: HTML — `NetworkFirst`, статика — `StaleWhileRevalidate`.
- `src/pwa-register.ts` регистрирует Service Worker **только** если: контекст `isSecureContext` (HTTPS/localhost) и приложение **не** в iframe/preview-домене. В противном случае все ранее зарегистрированные SW снимаются — это нужно, чтобы dev-preview и HTTP-развёртывания не залипали на устаревшем кэше.
- Monaco полностью локальный: `monaco-editor` + `editor.worker?worker`, `loader.config({ monaco })` — никаких CDN-загрузок, редактор работает в офлайн-режиме.

---

## 8. Дизайн-система

- Все цвета — семантические токены (`--background, --foreground, --primary, --muted, --accent, --signal-online/warning/offline/info ...`) в `src/styles.css`, формат `oklch(...)`.
- Запрещены литеральные цветовые классы (`bg-black`, `text-white`, и т.п.) в компонентах.
- Тёмная индустриальная палитра без градиентов на основном фоне; акценты — только на статусах и активных элементах.
- Графики используют `hsl(var(--primary))` и тонкую сетку.
- Никаких `Inter / Poppins` по умолчанию — типографика выровнена под индустриальный стиль.

---

## 9. Что не реализовано / план развития

- Реальный бэкенд: сейчас всё в `localStorage`, отправка пакетов на `targetUrl` не выполняется.
- Полноценная страница `/logs` (просмотрщик системных событий и журналов агентов).
- Drag-and-drop сегментов сценария, импорт/экспорт сценариев в JSON, сегмент «вызов скрипта», подсветка активного сегмента при проигрывании.
- Аутентификация и роли (предполагается через Lovable Cloud, ещё не подключено).
- Интеграция со SCADA-эндпоинтами и валидация ответов.

---

## 10. Журнал релизов

Канонический changelog лежит в `src/data/changelog.ts` (отображается в `ChangelogDialog`). Кратко:

- **0.4.0** — сворачиваемое меню, окно версии, хранилище УП, тёмная тема без градиентов, исправления гидратации `TimeAgo`.
- **0.3.0** — Monaco для `.csx`, IntelliSense по UniEmu Host API, тема `uniemu-dark`.
- **0.2.0** — раздел «Скрипты» (IDE), дерево файлов с группировкой, поиск, dirty-state.
- **0.1.0** — каркас приложения, главная, раздел «Эмуляторы», индустриальная тема.
