# UniEmu REST API и Runtime: дизайн первого среза

## Цель

Сделать backend для текущего фронта UniEmu Hub как модульный монолит на ASP.NET Core. Backend должен заменить seed/Zustand-данные из `UniEmu.Client/src/store/uniemu-store.ts`, хранить состояние в SQLite через EF Core и запускать runtime эмуляторов: генерацию телеметрии, запись истории и отправку пакетов на `targetUrl`.

## Границы первого среза

Входит:

- REST API по контракту из `UniEmu.Client/backend_endpoints.md`.
- CRUD для эмуляторов, тегов, скриптов, CNC-программ и событий.
- SQLite-хранилище через EF Core.
- История телеметрии в SQLite.
- `BackgroundService` для эмуляторов в статусе `Running`.
- Генерация значений для тегов с источниками `static`, `generator`, `scenario` на базовом уровне.
- Приём внешней телеметрии через `POST /api/telemetry/ingest`.
- Отправка runtime-пакетов на `targetUrl` через `HttpClient`.

Не входит в первый срез:

- Безопасное исполнение `.csx`-скриптов и inline-формул.
- Очереди, брокеры сообщений или выделение отдельных сервисов.
- Сложная агрегация временных рядов.
- Авторизация и многопользовательская модель.

Скрипты и формулы сохраняются и возвращаются фронту как данные, но runtime для них на первом этапе публикует последнее `preview`-значение.

## Архитектура

Проект остаётся одним ASP.NET Core Web API:

- `UniEmu/Data` — `UniEmuDbContext`, EF entities, конфигурации моделей, seed.
- `UniEmu/Features/Emulators` — API, DTO, сервисы для эмуляторов и статуса.
- `UniEmu/Features/Tags` — API, DTO, сервисы для тегов и вложенных конфигов.
- `UniEmu/Features/Scripts` — API и сервис хранения `.csx`.
- `UniEmu/Features/CncPrograms` — API и сервис хранения УП/G-code.
- `UniEmu/Features/Telemetry` — ingest, чтение истории, entity телеметрической точки.
- `UniEmu/Features/Events` — лента системных событий.
- `UniEmu/Runtime` — hosted service, генератор значений, отправитель пакетов.
- `UniEmu/Common` — общие DTO, `ProblemDetails`, clock/id helpers при необходимости.

Контроллеры остаются тонкими: валидация HTTP-входа, вызов сервиса, возврат DTO. Бизнес-правила и EF-запросы живут в feature-сервисах. Runtime не зависит от контроллеров и работает через scoped-сервисы/`DbContextFactory`.

## Модель данных

Основные таблицы:

- `Emulators`: `Id`, `Name`, `Status`, `TargetUrl`, `IntervalSec`, `LastRun`, `NextRun`, `LastError`, `StartedAt`, `TotalRequests`.
- `EmulatorTags`: `Id`, `EmulatorId`, `Name`, `Key`, `Type`, `Source`, `Preview`, `Description`, JSON-поля для `Trigger`, `Calc`, `Formula`, `Scenario`, `SpecialParameter`.
- `ScriptFiles`: `Id`, `Name`, `Scope`, `EmulatorId`, `Content`, `UpdatedAt`, `SizeBytes`.
- `CncPrograms`: `Id`, `Name`, `Scope`, `EmulatorId`, `Description`, `Content`, `SizeBytes`, `UpdatedAt`, `UploadedAt`, `IsBinary`.
- `TelemetryPoints`: `Id`, `EmulatorId`, `Timestamp`, `ValuesJson`.
- `SystemEvents`: `Id`, `EmulatorId`, `EmulatorName`, `Level`, `Message`, `Timestamp`.

Идентификаторы генерирует сервер. Для совместимости с фронтом DTO отдают строки. Вложенные конфиги тегов хранятся JSON-колонками, чтобы не усложнять первую схему большим количеством таблиц для настроек генерации.

## REST API

Первый срез реализует эндпоинты из `backend_endpoints.md`:

- `GET/POST/PATCH /api/emulators`
- `PATCH /api/emulators/{emulatorId}/status`
- `GET/POST/PATCH/DELETE /api/emulators/{emulatorId}/tags`
- `GET/POST/PATCH/DELETE /api/scripts`
- `GET/POST/PATCH/DELETE /api/cnc-programs`
- `POST /api/emulators/{emulatorId}/cnc-programs`
- `GET /api/emulators/{emulatorId}/telemetry?points={n}`
- `POST /api/telemetry/ingest`
- `GET/POST /api/events`

Ошибки возвращаются через стандартный `ProblemDetails`. Для отсутствующих сущностей используется `404`, для некорректных связок `scope/emulatorId` — `400`, для конфликтов имён в рамках одного scope — `409`, если такое ограничение будет введено.

## Runtime

`EmulatorRuntimeService : BackgroundService` выполняет цикл:

1. Периодически читает активные эмуляторы со статусом `Running`.
2. Для каждого эмулятора проверяет `NextRun` или рассчитывает запуск по `IntervalSec`.
3. Загружает теги эмулятора.
4. Вычисляет значения:
   - `static`: значение из `Preview`.
   - `generator`: базовая поддержка `Line`, `Random`, `Sinusoid`, `Square`, `Sawtooth`, `None`.
   - `scenario`: вычисление по сегментам и `continueOnFormulaEnd`.
   - `script`, `formula`, `cnc`: на первом этапе сохраняются как конфигурация; runtime публикует `Preview` до отдельного безопасного исполнителя.
5. Сохраняет `TelemetryPoint`.
6. Отправляет POST на `targetUrl` с телом `{ emulatorId, timestamp, values }`.
7. Обновляет `LastRun`, `NextRun`, `TotalRequests`, `LastError`.
8. Пишет системное событие об успехе или ошибке.

Старт/стоп через API меняет статус и расписание. Остановка не удаляет историю и не сбрасывает счётчики.

## Фронтовая интеграция

Фронт на первом этапе можно переводить с Zustand seed-store постепенно:

- Добавить API client с типами, совпадающими с `src/types/uniemu.ts`.
- Сначала подключить чтение списков и карточки эмулятора.
- Затем заменить мутации: create/update/toggle/tag/script/cnc.
- Локальный Zustand оставить как UI/cache слой или временный fallback, пока все страницы не переведены на API.

Ответы backend должны сохранять текущий casing значений enum там, где фронт уже ожидает строки: например `Running`, `Stopped`, `int`, `double`, `shared`, `emulator`, `info`.

## Проверка

Минимальная проверка первого среза:

- `dotnet build`.
- Интеграционные проверки основных CRUD-эндпоинтов.
- Проверка SQLite-миграции/создания базы.
- Проверка runtime: запуск эмулятора создаёт telemetry point, event и выполняет HTTP POST на тестовый handler.
- Проверка фронта после переключения API client: `npm run lint` и `npm run build`.

