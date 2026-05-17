---
tags:
  - uniemu
  - скрипты
  - monaco
  - api
---

# Редактор скриптов и API

Редактор скриптов в UniEmu - это встроенная IDE для `.csx`-скриптов. Он нужен, чтобы пользователь мог писать C#-логику вычисления тегов прямо в веб-консоли.

## Что можно делать в разделе Scripts

Пользователь может:

- смотреть дерево скриптов;
- создавать shared-скрипты;
- создавать скрипты, привязанные к эмулятору;
- переименовывать скрипты;
- удалять скрипты;
- редактировать содержимое;
- видеть dirty-state до сохранения;
- сохранять изменения в backend;
- использовать Monaco-подсветку и IntelliSense.

Скрипты хранятся в БД, не на файловой системе. Поэтому раздел Scripts - это не файловый менеджер сервера, а редактор записей `ScriptFiles`.

## Области видимости

| Scope | Что означает |
| --- | --- |
| `shared` | Скрипт виден всем эмуляторам. |
| `emulator` | Скрипт виден только выбранному эмулятору. |

При вычислении тега runtime видит shared-скрипты и скрипты текущего эмулятора. Это важно для `#load`: подключить можно только видимый скрипт.

## Возможности Monaco-редактора

Редактор использует Monaco с отдельным языком для CSX.

Включено:

- темная и светлая темы `uniemu-dark` / `uniemu-light`;
- C#-подсветка через semantic tokens;
- автодополнение;
- snippets;
- hover-документация;
- signature help при `(` и `,`;
- переход к определению;
- переход к type definition;
- поиск references;
- implementation provider;
- rename symbol;
- format document;
- format selection/range;
- folding ranges;
- diagnostics markers;
- bracket pair colorization;
- indentation guides;
- smooth scrolling;
- automatic layout;
- minimap отключен для компактности.

## Snippets

Клиентские snippets доступны в completion list:

| Snippet | Вставляет |
| --- | --- |
| `if` | `if (...) { ... }` |
| `ifelse` | `if (...) { ... } else { ... }` |
| `for` | цикл `for` |
| `foreach` | цикл `foreach` |
| `while` | цикл `while` |
| `try` | `try/catch` |
| `tryfinally` | `try/finally` |
| `using` | `using (...) { ... }` |
| `return` | `return value;` |
| `///` | XML summary comment |
| `/* */` | block comment |

## Backend IntelliSense

Monaco сам рисует интерфейс, но умные ответы приходят с backend:

- `POST /api/intellisense/csharp/diagnostics`;
- `POST /api/intellisense/csharp/completions`;
- `POST /api/intellisense/csharp/hover`;
- `POST /api/intellisense/csharp/signature-help`;
- `POST /api/intellisense/csharp/definition`;
- `POST /api/intellisense/csharp/type-definition`;
- `POST /api/intellisense/csharp/references`;
- `POST /api/intellisense/csharp/implementation`;
- `POST /api/intellisense/csharp/rename`;
- `POST /api/intellisense/csharp/format`;
- `POST /api/intellisense/csharp/format-range`;
- `POST /api/intellisense/csharp/folding-ranges`;
- `POST /api/intellisense/csharp/semantic-tokens`;
- `POST /api/intellisense/csharp/call-hierarchy/prepare`;
- `POST /api/intellisense/csharp/call-hierarchy/incoming`;
- `POST /api/intellisense/csharp/call-hierarchy/outgoing`.

Backend анализирует текущий unsaved source, видимые скрипты и document URI вида `uniemu://scripts/...`.

## Как скрипт возвращает значение

Скрипт обычно заканчивается `return ...;`.

Пример:

```csharp
var previous = UniEmu.State.PrevNumericValue ?? 0;
return previous + 1;
```

Backend нормализует последний `return expr;`, чтобы Roslyn script вернул значение.

Если скрипт ничего не задан, runtime использует `return null;`.

## Доступный API

Top-level доступны:

| Имя | Что это |
| --- | --- |
| `Now` | Текущее время вычисления тега. |
| `UniEmu` | Главный контекст UniEmu. |

Внутри `UniEmu`:

| Объект | Назначение |
| --- | --- |
| `UniEmu.Emulator` | Информация об эмуляторе. |
| `UniEmu.Tag` | Текущий вычисляемый тег. |
| `UniEmu.Tags` | Доступ к другим тегам. |
| `UniEmu.State` | Persistent state текущего скрипта. |
| `UniEmu.Rest` | Разрешенные REST-операции. |

## UniEmu.Emulator

Поля:

| Поле | Значение |
| --- | --- |
| `Id` | Идентификатор эмулятора. |
| `Name` | Имя эмулятора. |
| `Status` | Текущий статус. |
| `StartTime` | Время запуска, если известно. |

Пример:

```csharp
if (UniEmu.Emulator.Status != "Running")
{
    return 0;
}

return UniEmu.Emulator.Name;
```

## UniEmu.Tag

Поля:

| Поле | Значение |
| --- | --- |
| `Key` | Ключ текущего тега. |
| `Name` | Имя текущего тега. |
| `Value` | Текущее значение. |
| `Type` | Тип значения. |
| `Timestamp` | Время значения, если известно. |

Пример:

```csharp
return $"{UniEmu.Tag.Name}: {Now:O}";
```

## UniEmu.Tags

Методы и свойства:

| API | Что делает |
| --- | --- |
| `TryGetValue(string keyName, out TagScriptValue? tagValue)` | Читает другой тег по ключу. |
| `TrySetValue(string keyName, object? value)` | Пытается изменить static-тег. |
| `IsDirty` | Показывает, были ли изменения через accessor. |

Пример чтения другого тега:

```csharp
if (UniEmu.Tags.TryGetValue("SpindleLoad", out var spindle) &&
    spindle?.Value is double load)
{
    return load > 80 ? "High load" : "Normal";
}

return "No signal";
```

Пример изменения static-тега:

```csharp
UniEmu.Tags.TrySetValue("LastScriptRun", Now.ToString("O"));
return 1;
```

> [!IMPORTANT]
> `TrySetValue` предназначен для static-тегов. Не используйте его как универсальную замену нормальной модели данных.

## UniEmu.State

State живет между запусками скрипта. Это удобно для счетчиков, накопителей и памяти предыдущих решений.

Свойства:

| API | Что делает |
| --- | --- |
| `IsRunning` | У скрипта уже было хотя бы одно вычисление. |
| `PrevValue` | Предыдущее значение тега. |
| `PrevNumericValue` | Предыдущее значение как число, если возможно. |
| `PrevTimestamp` | Время предыдущего значения. |
| `IsDirty` | State изменился в текущем выполнении. |

Методы:

| API | Что делает |
| --- | --- |
| `Get(string key)` | Возвращает сохраненное значение как `TagScriptValue?`. |
| `Get<T>(string key, T? fallback = default)` | Возвращает значение с приведением типа. |
| `Set(string key, object? value)` | Сохраняет значение. |
| `Remove(string key)` | Удаляет значение. |
| `Clear()` | Очищает весь state. |
| `Snapshot()` | Возвращает словарь простых значений. |
| `this[string key]` | Индексатор для `Get(key)`. |

Пример счетчика:

```csharp
var count = UniEmu.State.Get<int>("count", 0) + 1;
UniEmu.State.Set("count", count);
return count;
```

Пример использования предыдущего значения:

```csharp
var prev = UniEmu.State.PrevNumericValue ?? 0;
return Math.Min(prev + 5, 100);
```

## UniEmu.Rest

REST API в скриптах ограничен заранее настроенным каталогом. Сейчас доступны:

| Метод | Что делает |
| --- | --- |
| `GetWorkerByIdAsync(int workerId)` | Возвращает работника по id или `null`. |
| `GetActiveWorkerAsync()` | Возвращает активного работника или `null`. |
| `RegisterWorkerAsync(int workerId)` | Регистрирует работника, при ошибке бросает исключение. |
| `TryRegisterWorkerAsync(int workerId)` | Возвращает `RestCallResult` без исключения при ошибке. |

`Worker` содержит:

- `Id`;
- `Name`;
- `Status`;
- `IsActive`.

`RestCallResult` содержит:

- `Success`;
- `StatusCode`;
- `Error`.

Пример:

```csharp
var worker = await UniEmu.Rest.GetActiveWorkerAsync();
return worker?.IsActive == true ? worker.Name : "No active worker";
```

Пример безопасной регистрации:

```csharp
var result = await UniEmu.Rest.TryRegisterWorkerAsync(42);
return result.Success ? "registered" : result.Error;
```

## #load и переиспользование кода

Скрипты могут использовать `#load` для подключения другого видимого скрипта:

```csharp
#load "shared/math.csx"

return CalculateLoad(10, 20);
```

Поддерживаются относительные пути от текущего виртуального файла. Backend проверяет циклы `#load`.

Запрещены:

```csharp
#r "..."
#using ...
```

## Ограничения безопасности

Запрещены unsafe/pointers и опасные API/пространства имен:

- `System.IO`;
- `System.Net`;
- `System.Reflection`;
- `System.Diagnostics`;
- `System.Threading`;
- `System.Runtime.InteropServices`;
- `System.Security`;
- `System.Environment`;
- `System.AppDomain`;
- `System.Activator`;
- `System.Type`;
- `System.GC`;
- `System.Console`.

Скрипты должны быть короткими и предсказуемыми. В текущей реализации нет полноценной sandbox-песочницы с лимитами CPU и памяти.

## Практические паттерны

### Накопительный счетчик

```csharp
var count = UniEmu.State.Get<int>("parts", 0);
count++;
UniEmu.State.Set("parts", count);
return count;
```

### Зависимость от другого тега

```csharp
if (!UniEmu.Tags.TryGetValue("EmergencyStop", out var emergency))
{
    return "unknown";
}

return emergency?.Value is true ? "alarm" : "ok";
```

### Ограничение generator-значения в FormulaScript

```csharp
var value = Convert.ToDouble(UniEmu.Tag.Value);
return Math.Clamp(value, 0, 100);
```

### Запоминание последнего статуса

```csharp
var status = UniEmu.State.Get<string>("status", "idle");

if (UniEmu.Tags.TryGetValue("SpindleLoad", out var loadTag) &&
    Convert.ToDouble(loadTag?.Value ?? 0) > 80)
{
    status = "busy";
}

UniEmu.State.Set("status", status);
return status;
```

## Когда использовать скрипт

Используйте скрипт, если:

- значение зависит от нескольких тегов;
- нужна память между вычислениями;
- нужна внешняя REST-операция;
- логика плохо описывается графиком;
- нужно менять static-теги как побочный эффект.

Не используйте скрипт для простого линейного роста или синусоиды. Для этого лучше [[14 Вычисление тегов#Функции генератора]] или [[15 Сценарии тегов]].

