---
tags:
  - uniemu
  - csx
  - scripting
---

# CSX-скрипты и IntelliSense

UniEmu позволяет вычислять теги через C# script (`.csx`). Скрипты хранятся в БД, редактируются во frontend через Monaco и выполняются backend runtime.

Эта страница описывает внутреннее устройство подсистемы. Пользовательское описание редактора, подсветки, snippets, IntelliSense и доступного API находится в [[16 Редактор скриптов и API]].

## Где живут скрипты

`.csx` не хранится как физический файл. Это запись `ScriptFileEntity`:

- `Id`;
- `Name`;
- `Scope`;
- `EmulatorId`;
- `Content`;
- `UpdatedAt`;
- `SizeBytes`.

Scope:

- `shared` - скрипт виден всем эмуляторам;
- `emulator` - скрипт виден только конкретному эмулятору.

При создании расширение `.csx` добавляется автоматически. Начальный шаблон содержит комментарий и `return 0;`.

## Как выбирается скрипт для тега

Для тега с `script`, `formula` или `formulaScript` runtime смотрит `TagFormulaConfig`:

1. Если есть `inlineScript`, используется он.
2. Если есть `scriptId`, берется сохраненный скрипт, видимый текущему эмулятору.
3. Если ничего не задано, выполняется `return null;`.

Для `formulaScript` сначала считается generator-значение, затем оно передается в скрипт как текущее значение.

## Выполнение

Перед запуском backend:

- нормализует последний `return expr;` в `expr`, чтобы Roslyn вернул значение;
- запрещает `#r` и `#using`;
- разрешает `#load`;
- раскрывает `#load` из видимых скриптов;
- проверяет циклы загрузки;
- валидирует безопасность;
- компилирует через `Microsoft.CodeAnalysis.CSharp.Scripting`;
- кеширует compiled script.

`#r` запрещен во всех формах, включая ссылку на локальную сборку и NuGet-пакет:

```csharp
#r "System.Text.Json.dll"
#r "nuget: Newtonsoft.Json, 13.0.3"
```

Запрет применяется в двух местах:

- при backend-анализе и сохранении скрипта через `CsxLanguageService`/`ScriptService`;
- при runtime-выполнении входного скрипта и всех видимых скриптов, которые могут быть загружены через `#load`.

Для IntelliSense и сохранения неподдерживаемые директивы возвращаются как ошибка `CSX001` до компиляции Roslyn. Это важно: backend не пытается резолвить DLL или NuGet-пакет, а сразу останавливает сценарий.

Кэш учитывает entry path, content, visible scripts, imports/references и globals type. При изменении скрипта `ScriptService` очищает кэш.

## Глобальный API скрипта

Публичная поверхность вынесена в проект `UniEmu.Scripting.Api`. Скрипту доступны:

| Объект | Назначение |
| --- | --- |
| `Now` | Текущее время. |
| `UniEmu.Emulator` | Контекст эмулятора. |
| `UniEmu.Tag` | Текущий тег. |
| `UniEmu.Tags` | Доступ к другим тегам и попытка изменить static-тег. |
| `UniEmu.State` | Persistent state скрипта. |
| `UniEmu.Rest` | Ограниченный REST-каталог. |

Доступные API маркируются атрибутом `[ScriptingApi]`, чтобы их можно было показывать в IntelliSense.

## Состояние скрипта

`UniEmu.State` хранит данные между запусками. На backend это таблица `ScriptRuntimeStates`.

Ключ:

- `inline:{tagId}` для inline-скрипта;
- `script:{scriptId}` для сохраненного скрипта.

State привязан к `EmulatorId`, поэтому один shared-скрипт может иметь разные состояния на разных эмуляторах.

## REST из скриптов

REST API скрипта не является свободным HTTP-клиентом. Это ограниченный набор операций из `TagScriptRestContext`:

- `GetWorkerByIdAsync(int)`;
- `GetActiveWorkerAsync()`;
- `RegisterWorkerAsync(int)`;
- `TryRegisterWorkerAsync(int)`.

Каталог операций читается из `UniEmu:RestCatalog` в конфигурации. Сейчас поддержаны GET/POST, base URL, timeout, headers и path templates.

Ошибки санитизируются: тело ответа и секреты не попадают в exception message.

## Безопасность

На уровне директив разрешен только `#load`. `#r`, включая `#r "System.Text.Json.dll"` и `#r "nuget: ..."` запрещен, потому что пользовательский скрипт не должен расширять набор сборок за пределы controlled reference set.

Validator блокирует unsafe/pointers и опасные namespaces/API:

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

> [!WARNING]
> Это защитный слой, а не полноценная изолированная песочница. В текущем коде нет жестких лимитов CPU и памяти для пользовательских скриптов.

## IntelliSense

Backend предоставляет endpoints:

- diagnostics;
- completions;
- hover;
- signature help;
- definition/type-definition;
- references/implementation;
- rename;
- format/format-range;
- folding ranges;
- semantic tokens;
- call hierarchy.

Ограничения:

- `SourceCode` до 20 000 символов;
- позиция курсора clamp до 10 000.

Frontend Monaco providers вызывают эти endpoints через fetch. Если provider получает ошибку, он обычно возвращает пустой результат, чтобы редактор не ломался.

Diagnostics использует тот же `CsxScriptDirectiveValidator`, что и runtime: неподдерживаемые директивы во входном документе или в загруженном видимом скрипте возвращаются как `CSX001`. Остальные ошибки идут из Roslyn compiler diagnostics и `CsxScriptSecurityValidator`.

## Практический пример идеи

Скриптовой тег может:

1. прочитать предыдущее значение из `UniEmu.State`;
2. посмотреть другой тег через `UniEmu.Tags`;
3. вызвать разрешенную REST-операцию;
4. изменить static-тег через side effect;
5. вернуть итоговое значение.

Такой подход подходит для сложных правил, счетчиков, внешних справочников и сценариев, которые неудобно выражать только генератором.
