---
tags:
  - uniemu
  - backend
  - документация
  - аудит
---

# Backend-аудит документации

Документ фиксирует состояние backend-документации после прохода по кодовой базе `UniEmu`, `UniEmu.Scripting.Api` и `UniEmu.DispatcherMock`.

## Область проверки

Проверялись:

- XML-документация C# (`summary`, `param`, `returns`) для публичной backend-поверхности.
- Markdown-документация в `README.md`, `ARCHITECTURE.md`, `docs/obsidian/*.md`, `docs/PROGRAM_DISPATCHER_FLOW.md`.
- Соответствие документации текущим backend-модулям: `Controllers`, `Features`, `Data`, `Domain`, `Runtime`, `Runtime/Scripting`, `Realtime`, `Hosting`, `DispatcherMock`.

## Что уже хорошо покрыто

| Тема | Где описано | Комментарий |
| --- | --- | --- |
| Общая архитектура | [[04 Архитектура]], `README.md` | Хорошо покрывает ASP.NET Core, EF Core/SQLite, Quartz runtime, SignalR и Dispatcher. |
| Домен и данные | [[05 Модель данных]], [[06 Эмуляторы и теги]] | Модель эмуляторов, тегов, CNC-программ, телеметрии и событий описана на уровне пользователя и разработчика. |
| REST API и realtime | [[10 REST API и realtime]] | Основные controllers и SignalR hub описаны достаточно полно. |
| Runtime и Dispatcher | [[07 Runtime и Dispatcher]], `docs/PROGRAM_DISPATCHER_FLOW.md` | Есть описание публикации, обмена файлами, `FileType`, `GetFile` и block-check. |
| Вычисление тегов | [[14 Вычисление тегов]], [[15 Сценарии тегов]] | Хорошо раскрыты generator/scenario/script и special parameters. |
| CSX API для пользователя | [[08 CSX-скрипты и IntelliSense]], [[16 Редактор скриптов и API]] | Публичный scripting API и редактор описаны лучше, чем внутренние Roslyn-сервисы. |

## Что закрыто XML-summary в коде

Добавлены русские XML-комментарии для участков, где `CS1591` показывал заметный долг:

- `Common/UniEmuJson.cs` - общие JSON-настройки и enum helpers.
- `Domain/Entities/*.cs` - сущности эмуляторов, тегов, скриптов, CNC-программ, телеметрии, событий и script state.
- `Data/UniEmuDbContext.cs`, `Data/CachedUniEmuDataService.cs`, `Data/UniEmuSeeder.cs` - EF-модель, кэш данных и seed-данные.
- `Mapping/UniEmuMapping.cs` - преобразование entity в DTO.
- `Runtime/TelemetryValueGenerator.cs`, `TelemetryPacketSender.cs` - генерация значений тегов и Dispatcher-протокол.
- `Runtime/TagRuntimeStateStore.cs`, `TagRuntimeStatePersistenceService.cs`, `TagPreviewFlushService.cs` - in-memory state, hydrate/persist и отложенная запись preview.
- `Runtime/RuntimeJobKeys.cs`, `EmulatorScheduleService.cs`, `TagValueJob.cs`, `DispatcherBlockCheckJob.cs` - Quartz-топология и задачи runtime.
- `Runtime/TagScriptExecutionService.cs`, `CompiledTagScriptCache.cs`, `DbScriptSourceResolver.cs` - выполнение и кэширование CSX-скриптов.
- `Runtime/Scripting/CsxLanguageService.cs`, `CsxDocumentContextParser.cs` - фасад backend IntelliSense, DTO language features и разбор контекста CSX-документа.
- `Runtime/Scripting/Environment/CsxScriptEnvironment.cs`, `CsxScriptSecurityValidator.cs`, `CsxScriptDirectiveValidator.cs`, `CsxLoadedScriptExpander.cs` - окружение Roslyn, controlled reference set, запрет `#r`/`#using`, проверка dangerous API и раскрытие `#load`.

Отдельно зафиксировано поведение CSX-директив: `#r "System.Text.Json.dll"` и `#r "nuget: Newtonsoft.Json, 13.0.3"` запрещены и возвращают `CSX001` на этапе диагностики/сохранения до компиляции Roslyn.

## Что еще требует документации

| Приоритет | Область | Что сделать |
| --- | --- | --- |
| P1 | `ARCHITECTURE.md` | Обновить устаревшие утверждения: миграции EF уже есть, startup использует `Database.MigrateAsync()`, часть старых имен файлов/сервисов не совпадает с текущим кодом. |
| P1 | `Runtime/Scripting` | Додокументировать оставшиеся `Services/*`, `Workspace/*`, `TagScriptContentNormalizer`, `CsxScriptValidationException`. `CsxLanguageService`, `Environment/*` и `CsxDocumentContextParser` уже покрыты русскими XML-summary. |
| P2 | Hosting | Добавить maintainer-summary про `Program.cs`, `UniEmuApplicationStartup`, `UniEmuServiceCollectionExtensions`, `UniEmuBackendServiceRegistration`, options, globalization и static assets. |
| P2 | Runtime internals | Вынести карту классов: `EmulatorScheduleService`, `EmulatorPublishJob`, `TagValueJob`, `DispatcherBlockCheckJob`, `TagRuntimeStateStore`, preview flush/persist. |
| P2 | Data/cache lifecycle | Описать, что хранится в SQLite, что живет in-memory, когда invalidates cache и когда preview сбрасывается в базу. |
| P2 | CSX internals | Дополнить карту source mapping и отдельных Roslyn service-классов. `#load` expansion, directive/security validation, script cache и отличие scripting API от backend internals уже описаны в [[08 CSX-скрипты и IntelliSense]]. |
| P3 | DispatcherMock | Зафиксировать endpoints mock-а и стандартные ответы: `/health`, `PostUniversalMonitoringDataJson`, `PostFileUniversal`, `GetFileUniversal`, `GetIsMonitoringBlocked`. |
| P3 | Realtime details | Дополнить группы SignalR и имена сообщений: `runtime:all`, `emulator:{id}`, `TelemetryPoint`, `TagValue`, `EmulatorUpdated`, `EventCreated`. |

## Практический вывод

Проект уже включает `GenerateDocumentationFile=true`, но предупреждение `CS1591` подавлено через `NoWarn`. Поэтому отсутствие XML-комментариев не видно в обычной сборке. Для периодического аудита полезна команда:

```powershell
$env:DOTNET_CLI_USE_MSBUILD_SERVER='0'
dotnet build UniEmu.Tests\UniEmu.Tests.csproj /t:Rebuild --no-restore -nr:false -p:UseSharedCompilation=false -p:SkipYarnBuild=True -p:NoWarn= /v:minimal
```

Обычная сборка может оставаться с подавлением `1591`, но перед релизом документации стоит запускать аудит без подавления и смотреть, какие новые публичные типы появились без русских `summary`.
