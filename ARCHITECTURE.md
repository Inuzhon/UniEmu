# UniEmu Architecture

UniEmu состоит из одного backend-монолита ASP.NET Core и frontend-приложения React/Vite. Цель архитектуры - сохранить простую монолитную поставку, но разделить код по функциональным зонам так, чтобы REST API, runtime эмуляторов, хранение УП и UI могли развиваться независимо внутри одного репозитория.

## Верхнеуровневая схема

```text
UniEmu.Client (React/Vite)
  |
  | HTTP /api/*
  v
UniEmu (ASP.NET Core Web API)
  |
  | EF Core
  v
SQLite

UniEmu Runtime
  |
  | HTTP Dispatcher protocol
  v
Dispatcher / SCADA
```

## Backend

Backend расположен в `UniEmu/`.

Основные обязанности:

- хранить конфигурацию эмуляторов, тегов, скриптов, УП, telemetry и events;
- отдавать REST API для frontend;
- запускать runtime эмуляторов;
- вычислять значения тегов;
- отправлять monitoring payload в Dispatcher;
- обмениваться управляющими программами с Dispatcher.

### Слои backend-а

`Program.cs`

- регистрирует controllers;
- настраивает JSON string enums;
- подключает EF Core SQLite;
- регистрирует feature services;
- регистрирует Quartz runtime;
- создает и seed-ит dev-базу через `EnsureCreatedAsync`;
- мапит static frontend assets и API controllers.

`Data`

- `UniEmuDbContext` содержит DbSet-ы и EF mapping.
- `UniEmuEntities.cs` содержит EF entities.
- `UniEmuSeeder.cs` наполняет dev-базу минимальными данными.

`Features`

Feature folders содержат HTTP controllers и services:

- `Features/Emulators` - CRUD эмуляторов, patch конфигурации, patch статуса.
- `Features/Tags` - CRUD тегов эмулятора.
- `Features/Scripts` - CRUD `.csx` скриптов.
- `Features/CncPrograms` - CRUD управляющих программ.
- `Features/Telemetry` - чтение telemetry history и ingest.
- `Features/Events` - чтение и создание системных событий.
- `Features/Contracts` - DTO, enums, JSON/mapping helpers.

Controllers должны оставаться тонкими. Бизнес-правила, EF-запросы и mapping должны жить в service/mapping слоях.

`Runtime`

Runtime отвечает за генерацию значений и публикацию данных.

Ключевые классы:

- `EmulatorScheduleService` управляет Quartz jobs для running эмуляторов.
- `EmulatorPublishJob` публикует monitoring payload для одного эмулятора.
- `TagValueJob` пересчитывает отдельный тег, если его trigger отличается от общего publish interval.
- `TagRuntimeStateStore` хранит последние значения тегов между jobs.
- `TelemetryValueGenerator` вычисляет значения тегов.
- `TelemetryPacketSender` отправляет monitoring payload и УП в Dispatcher, а также получает УП от Dispatcher.
- `RuntimeJobKeys` централизует Quartz job/trigger keys.
- `EmulatorRuntimeService` остался от раннего runtime-подхода и должен быть удален или явно помечен legacy, если Quartz окончательно выбран.

### Хранилище

Используется SQLite через EF Core.

Основные таблицы:

- `Emulators`
- `EmulatorTags`
- `ScriptFiles`
- `CncPrograms`
- `TelemetryPoints`
- `SystemEvents`

Вложенные настройки тегов (`Trigger`, `Calc`, `Formula`, `Scenario`) хранятся JSON-строками в `EmulatorTags`. Это упрощает первый срез, но требует аккуратной миграции, если потребуется сложный поиск по вложенным полям.

Текущий dev-режим использует `EnsureCreatedAsync`. Для стабильной схемы нужно перейти на EF migrations.

## REST API

Основной контракт описан в `UniEmu.Client/backend_endpoints.md`.

Ключевые endpoints:

- `GET /api/emulators`
- `GET /api/emulators/{emulatorId}`
- `POST /api/emulators`
- `PATCH /api/emulators/{emulatorId}`
- `PATCH /api/emulators/{emulatorId}/status`
- `GET /api/emulators/{emulatorId}/tags`
- `POST /api/emulators/{emulatorId}/tags`
- `PATCH /api/emulators/{emulatorId}/tags/{tagId}`
- `DELETE /api/emulators/{emulatorId}/tags/{tagId}`
- `GET /api/scripts`
- `POST /api/scripts`
- `PATCH /api/scripts/{scriptId}`
- `DELETE /api/scripts/{scriptId}`
- `GET /api/cnc-programs`
- `POST /api/cnc-programs`
- `POST /api/emulators/{emulatorId}/cnc-programs`
- `PATCH /api/cnc-programs/{programId}`
- `DELETE /api/cnc-programs/{programId}`
- `GET /api/emulators/{emulatorId}/telemetry?points={n}`
- `POST /api/telemetry/ingest`
- `GET /api/events`
- `POST /api/events`

OpenAPI доступен в development через встроенный `Microsoft.AspNetCore.OpenApi`. NSwag build-time generation не используется.

## Runtime и Dispatcher

Расширенный Dispatcher contract описан в `UniEmu/PROGRAM_DISPATCHER_FLOW.md`.

### Monitoring payload

Runtime отправляет POST на:

```text
/IndustryManagment/WebIntegration/PostUniversalMonitoringDataJson
```

Тело:

```json
{
  "MachineIntegrationId": 18,
  "UseInnerId": true,
  "ListValues": [
    { "Key": "PowerOn", "Value": true },
    { "Key": "6_Str1", "Value": "Main.nc" }
  ]
}
```

Важно:

- JSON должен быть PascalCase.
- `ListValues[].Key` берется из `EmulatorTag.Key`.
- Для UI telemetry values сохраняются по `EmulatorTag.Name`, чтобы графики не ломались от повторяющихся `Key`.
- `MachineIntegrationId` и `UseInnerId` пока имеют временную реализацию; их нужно вынести в модель эмулятора.

### Ответ Dispatcher

Ответ читается как текст. Runtime ищет маркеры substring-проверками:

- `FileType=1` - отправить основную УП.
- `FileType=2` - отправить подпрограмму.
- `GetFile=1` - получить УП от Dispatcher.

### Отправка УП

Runtime отправляет POST на:

```text
/IndustryManagment/WebIntegration/PostFileUniversal
```

Файл делится на блоки по `4096` байт:

- первый блок содержит `Hash = Base64(MD5(bytes))`;
- каждый блок содержит `FileUP = Base64(chunk)`;
- последний блок содержит `EOF = "1"`;
- `MachineIntegrationId` для отправки файла сериализуется строкой.

### Получение УП

Runtime вызывает:

```text
GET /IndustryManagment/WebIntegration/GetFileUniversal?machine_id={id}&file_type=1
GET /IndustryManagment/WebIntegration/GetFileUniversal?machine_id={id}&file_type=0
```

Сначала запрашивается `Hash=...`, затем блоки файла до строгого ответа `EOF`. После получения считается MD5 и сравнивается с ожидаемым.

Текущая реализация сохраняет полученный файл в `CncPrograms` как `[dispatcher-received]`. Для полной совместимости нужен byte-safe storage и явное разделение обычных и received программ.

## Frontend

Frontend расположен в `UniEmu.Client/`.

Основные части:

- `src/api/uniemu-api.ts` - ручной typed API client.
- `src/store/uniemu-store.ts` - Zustand cache/actions.
- `src/types/uniemu.ts` - TypeScript domain model.
- `src/routes/*` - страницы TanStack Router.
- `src/components/*` - layout, drawers, editors, UI.
- `src/components/tag-scenario/*` - сценарии и preview-математика.

Frontend не должен напрямую обращаться к `fetch` из страниц. Новые API-вызовы добавляются в `uniEmuApi`, затем в store action, затем используются компонентами.

## Dev взаимодействие frontend/backend

В dev:

- backend обычно слушает `http://localhost:5083`;
- frontend слушает `http://localhost:8070`;
- Vite proxy перенаправляет `/api` на backend;
- target можно переопределить через `VITE_API_PROXY_TARGET`.

В production/static hosting:

- frontend assets отдаются ASP.NET Core;
- API доступен по тому же origin через `/api`;
- `VITE_API_BASE_URL` можно использовать для отдельного API origin.

## Конфигурация

Backend:

- `ConnectionStrings:UniEmuDb` - SQLite connection string.
- `UniEmu:DisableRuntime` - отключает Quartz runtime.
- `UniEmu:SkipStartupDatabase` - пропускает startup database creation/seed.
- `UniEmu:DisableStaticAssets` - отключает static frontend assets и fallback.
- `UniEmu:EnableStaticAssetCompression` - enables runtime response compression for production static assets.
- `UniEmu:EnableStaticAssetCaching` - enables production static asset cache headers; `index.html` stays `no-cache`.

Frontend:

- `VITE_API_BASE_URL` - явный API base URL.
- `VITE_API_PROXY_TARGET` - dev proxy target.
- `VITE_PERSIST_STORE` - включает localStorage persist для части store.

## Известные архитектурные долги

- Нет EF migrations.
- Нет отдельного test project.
- `ProblemDetails` не унифицирован.
- Runtime имеет следы двух подходов: Quartz и старый hosted service.
- Dispatcher protocol settings не вынесены в модель эмулятора.
- УП хранятся как текст, а Dispatcher требует byte-safe обмен.
- `.csx` скрипты сохраняются, но не исполняются безопасно.
- Logs page на фронте еще placeholder.
- Полная frontend type/build проверка нестабильна из-за текущего dependency/tooling состояния.

