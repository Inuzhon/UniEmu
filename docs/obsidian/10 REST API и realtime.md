---
tags:
  - uniemu
  - api
  - signalr
---

# REST API и realtime

Backend предоставляет REST API для UI и SignalR hub для realtime-обновлений.

## Общие правила

- JSON enum-значения в API сериализуются строками.
- Ошибки сейчас не приведены к единому ProblemDetails-контракту.
- Аутентификация и `[Authorize]` в контроллерах не включены.
- Development OpenAPI мапится только в development.

## Emulators

Base path:

```text
/api/emulators
```

| Метод | Путь | Назначение |
| --- | --- | --- |
| `GET` | `/api/emulators` | Список эмуляторов. |
| `GET` | `/api/emulators/{emulatorId}` | Один эмулятор. |
| `GET` | `/api/emulators/{emulatorId}/dispatcher-template` | XML-шаблон Dispatcher. |
| `POST` | `/api/emulators` | Создать эмулятор. |
| `PATCH` | `/api/emulators/{emulatorId}` | Частично обновить настройки. |
| `PATCH` | `/api/emulators/{emulatorId}/status` | Изменить статус. |
| `DELETE` | `/api/emulators/{emulatorId}` | Удалить эмулятор. |

## Tags

Base path:

```text
/api/emulators/{emulatorId}/tags
```

| Метод | Путь | Назначение |
| --- | --- | --- |
| `GET` | `/api/emulators/{emulatorId}/tags` | Список тегов. |
| `POST` | `/api/emulators/{emulatorId}/tags` | Создать тег. |
| `PATCH` | `/api/emulators/{emulatorId}/tags/{tagId}` | Полностью заменить конфигурацию тега. |
| `DELETE` | `/api/emulators/{emulatorId}/tags/{tagId}` | Удалить тег. |

## Scripts

```text
/api/scripts
```

| Метод | Путь | Назначение |
| --- | --- | --- |
| `GET` | `/api/scripts?scope=&emulatorId=` | Список скриптов. |
| `POST` | `/api/scripts` | Создать `.csx`. |
| `PATCH` | `/api/scripts/{scriptId}` | Переименовать или обновить content. |
| `DELETE` | `/api/scripts/{scriptId}` | Удалить скрипт. |

## CNC programs

| Метод | Путь | Назначение |
| --- | --- | --- |
| `GET` | `/api/cnc-programs` | Список CNC-программ. |
| `POST` | `/api/cnc-programs` | Создать shared или явно scoped программу. |
| `POST` | `/api/emulators/{emulatorId}/cnc-programs` | Создать программу для эмулятора. |
| `PATCH` | `/api/cnc-programs/{programId}` | Обновить имя, описание или content. |
| `DELETE` | `/api/cnc-programs/{programId}` | Удалить программу. |

## Telemetry

| Метод | Путь | Назначение |
| --- | --- | --- |
| `GET` | `/api/emulators/{emulatorId}/telemetry?points={n}` | Последние telemetry points. |
| `POST` | `/api/telemetry/ingest` | Ручная запись telemetry point. |

`points` ограничен диапазоном 1..1000.

## Events

| Метод | Путь | Назначение |
| --- | --- | --- |
| `GET` | `/api/events?cursor=&limit=` | Лента событий. |
| `POST` | `/api/events` | Создать событие. |

`limit` ограничен диапазоном 1..200.

## IntelliSense API

Base path:

```text
/api/intellisense/csharp
```

Endpoints:

- `diagnostics`;
- `completions`;
- `hover`;
- `signature-help`;
- `definition`;
- `type-definition`;
- `references`;
- `implementation`;
- `rename`;
- `format`;
- `format-range`;
- `folding-ranges`;
- `semantic-tokens`;
- `call-hierarchy/prepare`;
- `call-hierarchy/incoming`;
- `call-hierarchy/outgoing`.

Подробно: [[08 CSX-скрипты и IntelliSense#IntelliSense]].

## SignalR

Hub:

```text
/hubs/runtime-updates
```

Группы:

- `runtime:all` - все подключенные клиенты;
- `emulator:{id}` - подписчики конкретного эмулятора.

Client methods:

- `SubscribeAll`;
- `SubscribeEmulator`;
- `UnsubscribeEmulator`.

Server events:

| Event | Payload |
| --- | --- |
| `TelemetryPoint` | `{ emulatorId, point }` |
| `TagValue` | `{ emulatorId, tagId, tagName, value, numericValue, timestamp }` |
| `EmulatorUpdated` | `EmulatorDto` |
| `EventCreated` | `SystemEventDto` |

Frontend применяет эти события к Zustand store.

Runtime публикует `EmulatorUpdated` не только при ручном изменении настроек или статуса. Ошибки отдельного `TagValueJob`, ошибки once-тегов и некорректные trigger/cron также обновляют `LastError` эмулятора и отправляют `EmulatorUpdated`, чтобы дашборд сразу пересчитал runtime-health без полного refresh.
