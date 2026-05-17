# UniEmu

**UniEmu** - это система для управления парком программных эмуляторов промышленного оборудования. Проект помогает имитировать работу станков, ЧПУ, линий, роботов и других устройств, публиковать телеметрию во внешний Dispatcher/SCADA по Universal-протоколу, хранить управляющие программы и описывать поведение тегов через статические значения, генераторы, сценарии и C#-скрипты.

Проект состоит из ASP.NET Core backend, React/Vite веб-консоли и отдельной библиотеки публичного scripting API. В одной поставке объединены REST API, realtime-обновления, runtime эмуляторов, SQLite-хранилище, Monaco-редактор `.csx`-скриптов и Docker/CI-инфраструктура.

---

## 📌 О проекте

UniEmu нужен там, где требуется проверить промышленную интеграцию без постоянного доступа к реальному оборудованию. Вместо физического станка или линии пользователь создает программный эмулятор: задает имя, протокольный идентификатор, адрес Dispatcher/SCADA, период публикации и набор тегов, которые должны уходить во внешний мониторинг.

Проект выступает как полноценная площадка для практики и разработки интерактивной промышленной эмуляции:

- проектирование доменной модели эмуляторов, тегов, telemetry, событий, скриптов и CNC-программ;
- разработка backend API на ASP.NET Core;
- хранение состояния через EF Core и SQLite;
- планирование runtime-задач через Quartz;
- обмен событиями в реальном времени через SignalR;
- интеграция с внешним Dispatcher/SCADA по HTTP-протоколу Universal;
- отправка и получение управляющих программ ЧПУ блочным протоколом;
- компиляция, анализ и выполнение пользовательских `.csx`-скриптов на базе Roslyn;
- реализация IntelliSense для Monaco Editor: diagnostics, completions, hover, signature help, navigation, rename, formatting, semantic tokens;
- разработка frontend-консоли на React/Vite с TanStack Router, Zustand, Monaco и Recharts;
- упаковка backend и frontend в единый Docker image.

---

## 🎯 Суть проекта и ключевые особенности

### Основная идея

UniEmu переносит проверку промышленной интеграции в управляемую программную среду. Вместо ожидания реального события на станке пользователь настраивает теги, генераторы или сценарии, запускает эмулятор и наблюдает, какие пакеты данных уходят во внешнюю систему.

Типовой сценарий:

1. Пользователь создает эмулятор станка или устройства.
2. Указывает `ProtocolId`, `TargetUrl` и интервал отправки телеметрии.
3. Добавляет теги: статические, генераторные, сценарные, CNC-зависимые или скриптовые.
4. При необходимости загружает CNC-программы и `.csx`-скрипты.
5. Запускает эмулятор.
6. Runtime пересчитывает значения тегов, сохраняет telemetry history, публикует payload в Dispatcher и отправляет realtime-обновления в UI.
7. Пользователь наблюдает графики, текущие значения, события и состояние обмена.

### Где это полезно

- отладка Dispatcher/SCADA-интеграций;
- проверка Universal-протокола мониторинга;
- демонстрация работы станков без физического стенда;
- воспроизведение аварий, остановов, изменения режимов и производственных сценариев;
- тестирование обмена управляющими программами ЧПУ;
- разработка и проверка формул, сценариев и скриптов расчета тегов.

---

## ✅ Текущие возможности

### Эмуляторы

- Создание, редактирование, запуск, остановка и удаление эмуляторов.
- Статусы `Running`, `Stopped`, `Error`, `Idle`.
- Настройка `ProtocolId`, целевого URL и интервала отправки.
- Счетчики uptime, количества запросов, времени последнего и следующего запуска.
- Генерация XML-шаблона Dispatcher для эмулятора.

### Теги и значения

- Типы значений: `int`, `double`, `string`, `bool`.
- Источники значений:
  - `static` - фиксированное значение;
  - `generator` - встроенный генератор;
  - `scenario` - таймлайн из нескольких сегментов;
  - `formula` - inline-формула или привязка к `.csx`;
  - `script` - отдельный `.csx`-скрипт;
  - `formulaScript` - генератор с дополнительной обработкой скриптом;
  - `cnc` - значение из контекста CNC-программы.
- Режимы запуска расчета:
  - `once` - при старте или остановке эмулятора;
  - `cron` - по cron-выражению;
  - `interval` - через заданный интервал в `ms`, `sec` или `min`.
- Включение/отключение тегов без удаления.
- Округление числовых значений.
- Описание тегов и специальные параметры Dispatcher-протокола.

### Генераторы и сценарии

Поддерживаемые типы генераторов:

- `None` - без генерации;
- `Text` - текстовое значение;
- `Line` - линейное изменение;
- `Curve` - кривая с коэффициентом кривизны;
- `Sequence` - последовательность значений;
- `Random` - случайное значение в диапазоне;
- `Sinusoid` - синусоидальный сигнал;
- `Square` - прямоугольный сигнал;
- `Sawtooth` - пилообразный сигнал;
- `SquircleEarly` и `SquircleLate` - сглаженные переходы.

Сценарный тег описывается как последовательность сегментов. Для конца сценария есть режимы `NoSignal`, `Zero`, `Repeat`, `Stretch`.

### Runtime

- Планирование фоновых задач через Quartz.
- Отдельные jobs для публикации эмулятора и пересчета тегов.
- In-memory runtime-state для последних значений.
- Персистентное состояние скриптов между вычислениями.
- Сохранение telemetry history.
- Создание системных событий.
- Остановка runtime через настройку `UniEmu:DisableRuntime`.

### Dispatcher / SCADA

- Отправка monitoring payload на Universal endpoint:

```text
/IndustryManagment/WebIntegration/PostUniversalMonitoringDataJson
```

- PascalCase JSON-контракт с `MachineIntegrationId`, `UseInnerId` и `ListValues`.
- Разбор текстового ответа Dispatcher по маркерам:
  - `FileType=1` - отправить основную УП;
  - `FileType=2` - отправить подпрограмму;
  - `GetFile=1` - получить УП от Dispatcher.
- Отправка CNC-файлов блоками по `4096` байт через:

```text
/IndustryManagment/WebIntegration/PostFileUniversal
```

- Передача `Hash` как Base64(MD5), `FileUP` как Base64 блока и `EOF = "1"` в последнем блоке.
- Получение файлов через:

```text
GET /IndustryManagment/WebIntegration/GetFileUniversal?machine_id={id}&file_type=1
GET /IndustryManagment/WebIntegration/GetFileUniversal?machine_id={id}&file_type=0
```

### Скрипты и IntelliSense

- Хранение `.csx`-скриптов с областью видимости `shared` или `emulator`.
- Публичный scripting API вынесен в проект `UniEmu.Scripting.Api`.
- Доступный скрипту контекст:
  - текущее время `Now`;
  - текущий тег;
  - другие теги;
  - данные эмулятора;
  - state между вычислениями;
  - ограниченный REST-каталог.
- Backend-сервисы Roslyn для Monaco Editor:
  - diagnostics;
  - completions;
  - hover;
  - signature help;
  - go to definition/type definition;
  - references;
  - implementation;
  - rename;
  - format / format range;
  - folding ranges;
  - semantic tokens;
  - call hierarchy.

### Веб-консоль

- Dashboard с KPI, активностью и состоянием эмуляторов.
- Список эмуляторов с фильтрацией и быстрыми действиями.
- Карточка эмулятора с overview, тегами, мониторингом и событиями.
- Редактор тегов с генераторами, сценариями и специальными параметрами.
- Monaco-редактор `.csx`-скриптов.
- Хранилище CNC-программ с загрузкой, просмотром и редактированием текстовых файлов.
- Recharts-графики telemetry и preview сценариев.
- PWA-манифест и Service Worker для кэширования frontend-ресурсов в защищенном контексте.

### Realtime

- SignalR hub:

```text
/hubs/runtime-updates
```

- UI получает runtime-события без ручного обновления страницы.
- Dev proxy Vite проксирует `/hubs` на backend с поддержкой WebSocket.

---

## 🧱 Технологии и архитектура

## Backend / Platform (.NET)

### Основной стек

- .NET 10
- ASP.NET Core Web API
- Controllers API
- OpenAPI в development-режиме
- Autofac
- Serilog
- Quartz

### Данные и инфраструктура

- Entity Framework Core
- SQLite
- EF migrations
- startup migration через `Database.MigrateAsync()`
- MemoryCache
- Docker / Docker Compose

### Runtime и scripting

- Quartz Hosted Service
- SignalR
- Roslyn:
  - `Microsoft.CodeAnalysis.CSharp`;
  - `Microsoft.CodeAnalysis.CSharp.Scripting`;
  - `Microsoft.CodeAnalysis.CSharp.Features`.
- Отдельный проект публичного scripting API: `UniEmu.Scripting.Api`.
- Изолированная поверхность API для пользовательских скриптов через атрибуты и отдельные контекстные классы.

### Observability и эксплуатация

- Serilog Console/File sinks
- Rolling file logs в `Logs/`
- Health-by-startup подход через инициализацию БД и runtime
- Конфигурация часового пояса и культуры приложения
- Response compression и cache headers для production static assets

### Архитектурный подход

UniEmu сохраняет простую монолитную поставку, но разделяет код по функциональным зонам:

- `Controllers` - тонкий HTTP-слой;
- `Features` - feature-сервисы и бизнес-операции;
- `Domain` - EF/domain entities;
- `Data` - `DbContext`, migrations, seed и cache доступа к данным;
- `Runtime` - фоновые jobs, генерация значений, Dispatcher exchange;
- `Runtime/Scripting` - CSX engine, Roslyn workspace, IntelliSense, REST-каталог;
- `Realtime` - SignalR hub и DTO realtime-событий;
- `Hosting` - DI, options, startup hooks, static assets.

Backend не разнесен на отдельные Clean Architecture-проекты намеренно: текущая цель - простая сборка и поставка одного приложения, при этом внутренние границы остаются достаточно явными.

---

## Frontend (React + Vite)

### Основной стек

- React 19 + TypeScript
- Vite
- TanStack Router
- Zustand
- Microsoft SignalR client
- Monaco Editor + `@monaco-editor/react`
- Recharts
- Tailwind CSS 4
- Radix UI wrappers
- lucide-react
- vite-plugin-pwa
- Yarn 1.22.22

### Архитектурные подходы

- Feature-oriented структура по маршрутам и предметным экранам.
- Ручной typed API client в `src/api/uniemu-api.ts`.
- Zustand store как UI/cache слой между API и компонентами.
- Компоненты не должны напрямую размазывать `fetch` по страницам.
- TanStack Router отвечает за маршруты и route tree.
- Monaco вынесен в отдельный компонентный слой `src/components/MonacoCsxEditor`.
- Математика сценариев вынесена в `tag-scenario/scenarioMath.ts`.
- UI и domain math разделены: страницы собирают пользовательский опыт, а расчетные функции остаются переиспользуемыми.
- PWA включается только в безопасном контексте: HTTPS или localhost.

---

## 🗂 Архитектурный срез репозитория

```text
UniEmu/
  Controllers/              # REST controllers
  Contracts/                # DTO и enum-контракты backend API
  Data/                     # EF Core DbContext, migrations, seed, cached data service
  Domain/Entities/          # доменные/EF-сущности
  Features/                 # feature-сервисы: emulators, tags, scripts, CNC, telemetry, events
  Hosting/                  # DI, options, startup, static assets
  Mapping/                  # mapping между entities и DTO
  Realtime/                 # SignalR hub и realtime DTO
  Runtime/                  # Quartz jobs, генерация telemetry, Dispatcher exchange
  Runtime/Scripting/        # CSX engine, Roslyn services, IntelliSense, REST catalog
  Program.cs                # composition root приложения
  appsettings*.json         # конфигурация backend
  Dockerfile                # production image backend + frontend

UniEmu.Scripting.Api/
  *.cs                      # публичная модель API, доступная пользовательским CSX-скриптам

UniEmu.Client/
  src/api/                  # typed API client
  src/components/           # layout, shared UI, Monaco CSX editor
  src/routes/               # страницы TanStack Router
  src/realtime/             # SignalR client
  src/store/                # Zustand store
  src/types/                # TypeScript domain model
  src/config/               # theme/PWA config
  public/                   # manifest и иконки

UniEmu.Tests/
  Features/                 # тесты feature-сервисов и controllers
  Runtime/                  # тесты runtime, scripting, Dispatcher exchange
  Hosting/                  # тесты startup/options/globalization
  Data/                     # тесты EF relationships и cache

docs/
  obsidian/                 # связанная база знаний по проекту
  superpowers/specs/        # design specs по реализованным изменениям
  superpowers/plans/        # implementation plans
  PROGRAM_DISPATCHER_FLOW.md
  ci-cd.md

docker-compose.yml
ARCHITECTURE.md
UniEmu.slnx
```

---

## 🧩 Доменная модель

### Эмулятор

Эмулятор представляет виртуальный станок или устройство. Основные поля:

- `id` - идентификатор;
- `name` - отображаемое имя;
- `status` - рабочий статус;
- `protocolId` - идентификатор протокола/станка для Dispatcher;
- `targetUrl` - адрес внешней системы;
- `intervalSec` - период публикации;
- `lastRun`, `nextRun`, `lastError`;
- `tagsCount`, `uptimeSec`, `totalRequests`.

### Тег

Тег - отдельное значение, которое попадает в telemetry payload. Важные поля:

- `name` - человекочитаемое имя;
- `key` - ключ, который отправляется во внешний payload;
- `type` - тип значения;
- `source` - источник вычисления;
- `preview` - начальное или последнее известное значение;
- `trigger` - режим запуска расчета;
- `calc` - параметры генератора;
- `formula` - inline или linked `.csx`;
- `scenario` - timeline-сценарий;
- `enabled` - участвует ли тег в публикации;
- `roundDigits` - округление;
- `specialParameter` - семантика для Dispatcher/CNC-обмена.

### CNC-программа

CNC-программа хранит управляющий файл:

- shared для всего парка эмуляторов;
- emulator-scoped для конкретного эмулятора.

Текущая модель хранит content как строку и флаг `isBinary`. Для полной совместимости с бинарными файлами Dispatcher в roadmap остается byte-safe storage.

### Скрипт

Скрипт - `.csx` файл, доступный тегам. Он может быть общим или привязанным к конкретному эмулятору. Скрипты анализируются через Roslyn и могут участвовать в расчете значений тегов.

### Telemetry и события

- `TelemetryPoint` хранит timestamp и значения тегов.
- `SystemEvent` хранит уровень, сообщение, связанный эмулятор и timestamp.

---

## 🔌 REST API

Backend предоставляет REST endpoints под префиксом `/api`.

### Эмуляторы

```http
GET    /api/emulators
GET    /api/emulators/{emulatorId}
GET    /api/emulators/{emulatorId}/dispatcher-template
POST   /api/emulators
PATCH  /api/emulators/{emulatorId}
PATCH  /api/emulators/{emulatorId}/status
DELETE /api/emulators/{emulatorId}
```

### Теги

```http
GET    /api/emulators/{emulatorId}/tags
POST   /api/emulators/{emulatorId}/tags
PATCH  /api/emulators/{emulatorId}/tags/{tagId}
DELETE /api/emulators/{emulatorId}/tags/{tagId}
```

### Скрипты

```http
GET    /api/scripts
POST   /api/scripts
PATCH  /api/scripts/{scriptId}
DELETE /api/scripts/{scriptId}
```

### CNC-программы

```http
GET    /api/cnc-programs
POST   /api/cnc-programs
POST   /api/emulators/{emulatorId}/cnc-programs
PATCH  /api/cnc-programs/{programId}
DELETE /api/cnc-programs/{programId}
```

### Telemetry и события

```http
GET  /api/emulators/{emulatorId}/telemetry
POST /api/telemetry/ingest
GET  /api/events
POST /api/events
```

### IntelliSense

```http
POST /api/intellisense/csharp/diagnostics
POST /api/intellisense/csharp/completions
POST /api/intellisense/csharp/hover
POST /api/intellisense/csharp/signature-help
POST /api/intellisense/csharp/definition
POST /api/intellisense/csharp/type-definition
POST /api/intellisense/csharp/references
POST /api/intellisense/csharp/implementation
POST /api/intellisense/csharp/rename
POST /api/intellisense/csharp/format
POST /api/intellisense/csharp/format-range
POST /api/intellisense/csharp/folding-ranges
POST /api/intellisense/csharp/semantic-tokens
POST /api/intellisense/csharp/call-hierarchy/prepare
POST /api/intellisense/csharp/call-hierarchy/incoming
POST /api/intellisense/csharp/call-hierarchy/outgoing
```

В development-окружении OpenAPI доступен через встроенный ASP.NET Core OpenAPI endpoint.

---

## 🚀 Локальный запуск

### Требования

- .NET SDK 10
- Node.js 20+ или актуальный LTS, совместимый с frontend-зависимостями
- Yarn 1.x, в проекте указан `yarn@1.22.22`
- Docker Desktop, если нужен контейнерный запуск
- Visual Studio / Rider / VS Code по желанию

### 1. Установка frontend-зависимостей

```powershell
cd UniEmu.Client
yarn install
```

### 2. Запуск backend

Из корня репозитория:

```powershell
dotnet run --project UniEmu/UniEmu.csproj
```

По умолчанию backend слушает:

```text
http://localhost:5083
```

Порт задается настройкой:

```json
{
  "UniEmu": {
    "Port": 5083
  }
}
```

### 3. Запуск frontend

В отдельном терминале:

```powershell
cd UniEmu.Client
yarn dev
```

Vite dev server слушает:

```text
http://localhost:8070
```

В dev-режиме Vite проксирует:

- `/api` на `http://localhost:5083`;
- `/hubs` на `http://localhost:5083` с WebSocket.

Целевой backend можно переопределить:

```powershell
$env:VITE_API_PROXY_TARGET="http://localhost:5083"
yarn dev
```

### 4. Первый сценарий в UI

1. Откройте `http://localhost:8070`.
2. Перейдите в раздел эмуляторов.
3. Создайте эмулятор с именем, `ProtocolId`, `TargetUrl` и интервалом отправки.
4. Добавьте несколько тегов.
5. Для числовых тегов настройте генератор или сценарий.
6. Запустите эмулятор.
7. Откройте карточку эмулятора и наблюдайте telemetry, графики и события.

---

## 🐳 Docker Compose

Из корня репозитория:

```powershell
docker compose up --build
```

Compose поднимает сервис `uniemu`, собирает image из `UniEmu/Dockerfile`, публикует порт `5083` и сохраняет данные в Docker volumes.

### Переменные окружения Compose

| Переменная | Значение по умолчанию | Назначение |
| --- | --- | --- |
| `UNIEMU_IMAGE` | `uniemu:local` | Имя Docker image |
| `UNIEMU_CONTAINER_NAME` | `uniemu` | Имя контейнера |
| `BUILD_CONFIGURATION` | `Release` | Конфигурация сборки .NET |
| `ASPNETCORE_ENVIRONMENT` | `Production` | ASP.NET Core environment |
| `UNIEMU_PORT` | `5083` | Внешний порт |
| `UNIEMU_TIME_ZONE` | `Europe/Moscow` | Часовой пояс приложения |
| `UNIEMU_DB_CONNECTION` | `Data Source=/app/data/uniemu.db` | SQLite connection string |

### Volumes

```text
uniemu-data -> /app/data
uniemu-logs -> /app/Logs
```

---

## ⚙️ Конфигурация

### Backend

Основные настройки находятся в `UniEmu/appsettings.json` и `UniEmu/appsettings.Development.json`.

```json
{
  "ConnectionStrings": {
    "UniEmuDb": "Data Source=uniemu.db"
  },
  "UniEmu": {
    "Port": 5083,
    "TimeZone": "Europe/Moscow",
    "Culture": "ru-RU",
    "DispatcherBlockCheckIntervalSeconds": 5,
    "EnableStaticAssetCompression": true,
    "EnableStaticAssetCaching": true
  }
}
```

Важные ключи:

| Ключ | Назначение |
| --- | --- |
| `ConnectionStrings:UniEmuDb` | SQLite connection string |
| `UniEmu:Port` | HTTP-порт backend |
| `UniEmu:TimeZone` | Часовой пояс runtime |
| `UniEmu:Culture` | Культура приложения |
| `UniEmu:DisableRuntime` | Отключает Quartz runtime |
| `UniEmu:SkipStartupDatabase` | Пропускает startup database initialization |
| `UniEmu:DisableStaticAssets` | Отключает отдачу статического frontend |
| `UniEmu:EnableStaticAssetCompression` | Включает Brotli/Gzip compression |
| `UniEmu:EnableStaticAssetCaching` | Включает cache headers для production assets |
| `UniEmu:RestCatalog` | Каталог разрешенных REST-операций для CSX-скриптов |

### Frontend

| Переменная | Назначение |
| --- | --- |
| `VITE_API_BASE_URL` | Явный origin backend API |
| `VITE_API_PROXY_TARGET` | Target для Vite dev proxy |
| `VITE_PERSIST_STORE` | Включение localStorage persist для части store |

Если `VITE_API_BASE_URL` не задан, frontend использует относительные `/api/...` и `/hubs/...`.

---

## 🧪 Проверка и тесты

### Backend tests

Рекомендуемая команда из корня репозитория:

```powershell
$env:DOTNET_CLI_USE_MSBUILD_SERVER='0'
dotnet test UniEmu.Tests/UniEmu.Tests.csproj --no-restore -nr:false -p:UseSharedCompilation=false -p:SkipYarnBuild=True
```

Если CLI-сборка зависает из-за фоновых build servers:

```powershell
dotnet build-server shutdown
```

### Backend build

```powershell
$env:DOTNET_CLI_USE_MSBUILD_SERVER='0'
dotnet build UniEmu.Tests/UniEmu.Tests.csproj --no-restore -nr:false -p:UseSharedCompilation=false -p:SkipYarnBuild=True /v:minimal
```

### Frontend

```powershell
cd UniEmu.Client
yarn lint
yarn build
```

В проектных заметках указано, что `tsc --noEmit` в текущем tooling-состоянии может зависать, поэтому его не стоит считать обязательной быстрой проверкой без дополнительной диагностики.

---

## 📦 Сборка и поставка

### Production Docker image

`UniEmu/Dockerfile` собирает приложение в несколько stages:

1. `client-build` на `node:20-bookworm-slim`:
   - `yarn install --frozen-lockfile`;
   - `yarn build`.
2. `backend-build` на `mcr.microsoft.com/dotnet/sdk:10.0`:
   - `dotnet restore`;
   - копирование frontend `dist` в `UniEmu/wwwroot`;
   - `dotnet publish` с `SkipYarnBuild=True`.
3. `final` на `mcr.microsoft.com/dotnet/aspnet:10.0`:
   - запуск `dotnet UniEmu.dll`;
   - данные в `/app/data`;
   - логи в `/app/Logs`.

### GitLab CI/CD

Корневой `.gitlab-ci.yml` содержит stages:

- `test`;
- `package`;
- `publish`.

Pipeline выполняет:

- restore/build/test backend;
- сборку frontend;
- сборку Docker image;
- публикацию image в registry;
- сборку Windows `win-x64` publish artifact.

Подробности описаны в `docs/ci-cd.md`.

---

## 📚 Документация

В проекте есть несколько уровней документации:

| Файл / каталог | Назначение |
| --- | --- |
| `README.md` | Главный обзор проекта и быстрый вход |
| `ARCHITECTURE.md` | Архитектурная карта backend/frontend/runtime |
| `docs/obsidian/` | Obsidian-совместимая база знаний |
| `docs/obsidian/02 Быстрый старт.md` | Подробный быстрый старт |
| `docs/obsidian/04 Архитектура.md` | Подробная архитектура |
| `docs/obsidian/07 Runtime и Dispatcher.md` | Runtime и обмен с Dispatcher |
| `docs/obsidian/08 CSX-скрипты и IntelliSense.md` | Scripting subsystem |
| `docs/obsidian/10 REST API и realtime.md` | REST API и SignalR |
| `docs/PROGRAM_DISPATCHER_FLOW.md` | Контракт обмена УП и monitoring payload |
| `docs/ci-cd.md` | Docker packaging и GitLab pipeline |
| `docs/superpowers/specs/` | Design specs по отдельным изменениям |
| `docs/superpowers/plans/` | Implementation plans |

Для чтения связанной базы знаний можно открыть `docs/obsidian` как Obsidian vault.

---

## 🧭 Соглашения разработки

### Backend

- Controllers должны оставаться тонким delivery-слоем.
- Бизнес-логика, EF-запросы и mapping живут в `Features`, `Data` и `Mapping`.
- Новые runtime-задачи должны регистрироваться через Quartz и использовать централизованные job/trigger keys.
- Новые API-контракты должны обновлять DTO/enums в `Contracts`.
- Новые scripting-возможности должны проходить через `UniEmu.Scripting.Api`, чтобы IntelliSense и runtime видели одну публичную поверхность.
- Для пользовательских скриптов нельзя неявно расширять доступ к небезопасным API.

### Frontend

- Новые backend-backed операции добавляются в `src/api/uniemu-api.ts`, затем в `src/store/uniemu-store.ts`.
- Компоненты работают со store/actions, а не с прямым `fetch`.
- Domain math сценариев остается в `tag-scenario/scenarioMath.ts`.
- UI-компоненты сначала ищутся в `src/components/ui`.
- Route tree не редактируется вручную, если он генерируется tooling-ом.
- Ошибки backend желательно выводить через store `apiError` или toast.

---

## ⚠️ Известные ограничения и roadmap

Текущее состояние проекта уже покрывает основной поток управления эмуляторами, тегами, runtime и UI, но есть технические зоны для развития:

- byte-safe storage для CNC-программ, чтобы надежно хранить и отдавать бинарные файлы;
- полноценная унификация `ProblemDetails` и ошибок API;
- дополнительное усиление sandbox/политик пользовательских `.csx`-скриптов;
- развитие auth/roles, если проект будет использоваться несколькими группами пользователей;
- расширение observability: health checks, metrics, traces;
- стабилизация frontend type-check/tooling workflow, если потребуется обязательный `tsc --noEmit`.

---

## 🧠 Коротко для нового разработчика

1. Backend запускается из `UniEmu/UniEmu.csproj` и слушает порт `5083`.
2. Frontend запускается из `UniEmu.Client` командой `yarn dev` и слушает порт `8070`.
3. В dev frontend проксирует `/api` и `/hubs` на backend.
4. Основная БД - SQLite, connection string лежит в `ConnectionStrings:UniEmuDb`.
5. Runtime работает через Quartz и отключается настройкой `UniEmu:DisableRuntime`.
6. Реальное поведение тегов ищите в `UniEmu/Runtime`.
7. REST API - в `UniEmu/Controllers` и feature-сервисах.
8. Monaco/CSX IntelliSense - в `UniEmu/Runtime/Scripting` и `UniEmu.Client/src/components/MonacoCsxEditor`.
9. UI-страницы - в `UniEmu.Client/src/routes`.
10. Самая подробная проектная документация - в `docs/obsidian`.
