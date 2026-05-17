# Логика обмена УП и отправки данных в Dispatcher

Документ описывает текущее поведение универсального протокола эмулятора: отправку/получение управляющих программ (УП, `program`) и цикл `update_data_for_dispatcher`. Цель - дать достаточный контракт для реализации аналогичной логики на C#.

## Термины

- **Dispatcher** - HTTP-сервер, принимающий мониторинговые данные и управляющий обменом УП.
- **УП / program / FileUP** - файл управляющей программы ЧПУ, который передается между эмулятором станка и Dispatcher.
- **Основная программа** - УП, имя которой приходит из параметра со `SpecialParameter = "PrgName"`.
- **Подпрограмма** - УП, имя которой приходит из параметра со `SpecialParameter = "Subprogram"`.
- **MachineIntegrationId** - идентификатор станка/протокола из конфигурации `ProtocolIntegrationId`.
- **UseInnerId** - флаг из конфигурации. Передается в JSON-запросах к Dispatcher как есть.
- **ListValues** - массив пар `{ "Key": "...", "Value": ... }`, через который передаются мониторинговые параметры, блоки файла и служебные маркеры.

## Конфигурация и состояние

Для каждого станка создается экземпляр универсального протокола на основе элемента `ProtocolSettings`.

Важные поля конфигурации:

```json
{
  "DispatcherServerInfo": {
    "ServerAddress": "192.168.5.200",
    "ServerPort": "8080"
  },
  "IsUseHistoryLoad": false,
  "LoadHistoryInterval": 1,
  "ProtocolSettings": [
    {
      "ProtocolIntegrationId": 18,
      "UseInnerId": true,
      "ProgramDirectory": "010",
      "SendInterval": 1,
      "HistoryLoadHours": 600,
      "ContinueOnFormulaEnd": "NoSignal",
      "ParametersList": []
    }
  ]
}
```

Состояние протокола:

- `last_date_sent` - timestamp последней успешно отправленной точки данных. При старте равен `time_emulator_start - HistoryLoadHours * 3600`.
- `cycle_length` - максимальная суммарная длительность формул среди всех параметров. Используется как длина повторяющегося цикла эмуляции.
- `parameters_list` - список параметров станка из `ParametersList`.
- `protocol_program_directory_path` - каталог программ станка: `Programs/<ProgramDirectory>`.
- `received_programs_directory_path` - каталог полученных от Dispatcher программ: `Programs/<ProgramDirectory>/Received_Programs`.
- `program_cache` - общий кэш байтов файлов УП по пути файла.

## Модель параметров мониторинга

Каждый параметр в `ParametersList` имеет минимум:

```json
{
  "Key": "6_Str1",
  "Name": "Program Name",
  "DataType": "String",
  "SpecialParameter": "PrgName",
  "Values": [
    {
      "Formula": "Text",
      "Start": "Main.nc",
      "Finish": null,
      "Distortion": 0,
      "Duration": 100
    }
  ]
}
```

Используемые поля:

- `Key` - имя параметра, которое отправляется в Dispatcher.
- `DataType` - тип значения: `Boolean`, `Integer`, `Float`, `String`, `None`.
- `Values` - список формул по времени. По ним вычисляется текущее значение.
- `SpecialParameter` - необязательная семантика параметра. Для обмена УП важны:
  - `PrgName` - значение параметра считается именем основной УП.
  - `Subprogram` - значение параметра считается именем подпрограммы.

Приведение типов перед отправкой:

- `Boolean` -> boolean.
- `Integer` -> integer.
- `Float` -> floating point number.
- `String` -> string.
- `None` или исходное значение `null` -> `null`.

## Расчет времени отправки

`update_data_for_dispatcher(current_time, use_history_load, load_history_interval)` на каждом вызове выбирает, какую временную точку отправлять.

Если включена загрузка истории и накопленное отставание больше двух интервалов отправки:

```text
use_history_load == true
AND last_date_sent < current_time - 2 * SendInterval
```

то отправляется историческая точка:

- `send_time = last_date_sent`.
- В `ListValues` дополнительно добавляется `{ "Key": "MDCReadDate", "Value": "YYYY MM DD;HH:MM:SS" }`.
- `new_last_date_sent = last_date_sent + SendInterval`.
- После итерации ждать `LoadHistoryInterval`.

Иначе отправляется актуальная точка:

- `send_time = current_time`.
- `new_last_date_sent = current_time`.
- После итерации ждать `SendInterval`.

Положение внутри цикла эмуляции:

```text
current_time_in_cycle = (send_time - (time_emulator_start % cycle_length)) % cycle_length
```

По этому времени для каждого параметра выбирается активная формула и вычисляется значение.

## Контракт отправки мониторинговых данных

### Endpoint

```http
POST http://{ServerAddress}:{ServerPort}/IndustryManagment/WebIntegration/PostUniversalMonitoringDataJson
Content-Type: application/json
```

### Request body

```json
{
  "MachineIntegrationId": 18,
  "UseInnerId": true,
  "ListValues": [
    { "Key": "MDCReadDate", "Value": "2026 05 07;21:27:00" },
    { "Key": "panelMode", "Value": 0 },
    { "Key": "PowerOn", "Value": true },
    { "Key": "6_Str1", "Value": "Main.nc" },
    { "Key": "subProgName", "Value": "SubProg_1.nc" }
  ]
}
```

`MDCReadDate` присутствует только при отправке исторических данных.

### Response body

Текущий клиент ожидает простой текст и ищет в нем маркеры через substring-проверки. Формального JSON-ответа нет.

Поддерживаемые маркеры:

- `FileType=1` - Dispatcher запрашивает у станка основную УП.
- `FileType=2` - Dispatcher запрашивает у станка подпрограмму.
- `GetFile=1` - Dispatcher готов отдать файл станку, клиент должен скачать УП.
- `FileType=0` и `GetFile=0` - обычная отправка данных без обмена файлами.

Маркеры могут приходить в одной строке вместе с другими данными. Логика проверяет наличие подстрок, а не точное равенство.

## Выбор файла УП для отправки

Во время формирования `ListValues` протокол отслеживает параметры со специальными типами.

Для основной УП:

1. Если у параметра `SpecialParameter = "PrgName"` вычисленное значение truthy/непустое, значение приводится к строке и считается именем файла.
2. Сначала файл ищется в `Programs/<ProgramDirectory>/Received_Programs/<имя>`.
3. Если там файла нет, используется `Programs/<ProgramDirectory>/<имя>`.
4. Полученный путь сохраняется как путь основной программы для возможного ответа на `FileType=1`.

Для подпрограммы алгоритм такой же, но используется параметр `SpecialParameter = "Subprogram"` и результат сохраняется как путь подпрограммы для `FileType=2`.

Важно: если текущая итерация не вычислила имя программы или файл отсутствует, путь может быть `null` или указывать на несуществующий файл. Текущая реализация явно игнорирует только `null`; отсутствие файла при чтении приведет к ошибке чтения. В C# лучше отдельно решить, нужно ли логировать и пропускать несуществующий файл.

## Отправка УП в Dispatcher

Отправка запускается после ответа на мониторинговые данные:

- `FileType=1` -> отправить основную УП.
- `FileType=2` -> отправить подпрограмму.

### Endpoint

```http
POST http://{ServerAddress}:{ServerPort}/IndustryManagment/WebIntegration/PostFileUniversal
Content-Type: application/json
```

### Алгоритм

1. Если путь файла `null`, ничего не делать.
2. Прочитать весь файл в байты. В Python байты кэшируются в `program_cache` по пути файла.
3. Посчитать MD5 от всех байтов файла.
4. Закодировать MD5 digest в Base64. Это значение передается как `Hash`.
5. Разбить файл на блоки по `buffer_size`, сейчас используется `4096` байт.
6. Каждый блок закодировать в Base64 и отправить отдельным POST-запросом.
7. В первый POST дополнительно добавить `Hash`.
8. В последний POST дополнительно добавить `{ "Key": "EOF", "Value": "1" }`.
9. Ответ сервера читается как текст, но сейчас не валидируется.

### Request body: первый блок

```json
{
  "MachineIntegrationId": "18",
  "UseInnerId": true,
  "ListValues": [
    { "Key": "Hash", "Value": "base64-md5-digest" },
    { "Key": "FileUP", "Value": "base64-file-chunk" }
  ]
}
```

### Request body: промежуточный блок

```json
{
  "MachineIntegrationId": "18",
  "UseInnerId": true,
  "ListValues": [
    { "Key": "FileUP", "Value": "base64-file-chunk" }
  ]
}
```

### Request body: последний блок

```json
{
  "MachineIntegrationId": "18",
  "UseInnerId": true,
  "ListValues": [
    { "Key": "FileUP", "Value": "base64-file-chunk" },
    { "Key": "EOF", "Value": "1" }
  ]
}
```

Если файл меньше или равен `4096` байт, один и тот же запрос будет первым и последним:

```json
{
  "MachineIntegrationId": "18",
  "UseInnerId": true,
  "ListValues": [
    { "Key": "Hash", "Value": "base64-md5-digest" },
    { "Key": "FileUP", "Value": "base64-file-chunk" },
    { "Key": "EOF", "Value": "1" }
  ]
}
```

### Особенности для C#

- Использовать `MD5.HashData(bytes)` или потоковый MD5, затем `Convert.ToBase64String(hashBytes)`.
- Для блока использовать исходные байты файла, не текстовое содержимое.
- Для Base64 блока использовать `Convert.ToBase64String(chunkBytes)`.
- `MachineIntegrationId` в отправке файла текущий Python-код сериализует как строку, хотя при мониторинге отправляет числом. Для полной совместимости можно отправлять строку.
- Запросы отправляются последовательно, порядок блоков важен.

## Получение УП от Dispatcher

Получение запускается после ответа `PostUniversalMonitoringDataJson`, если в тексте ответа есть `GetFile=1`.

Текущий код сохраняет файл как:

```text
Programs/<ProgramDirectory>/Received_Programs/received_program_machine_id_<MachineIntegrationId>.txt
```

Имя файла не берется из ответа Dispatcher.

### Endpoint

```http
GET http://{ServerAddress}:{ServerPort}/IndustryManagment/WebIntegration/GetFileUniversal?machine_id={MachineIntegrationId}&file_type={fileType}
```

### Шаг 1: запрос хеша

Сначала вызывается:

```http
GET /IndustryManagment/WebIntegration/GetFileUniversal?machine_id=18&file_type=1
```

Ожидаемый текстовый ответ:

```text
Hash=<base64-md5-digest>
```

Если ответ содержит `Hash=`, клиент берет все символы начиная с позиции 5 как Base64-хеш и начинает прием файла. Иначе прием не начинается.

### Шаг 2: запрос блоков файла

Пока прием активен, клиент циклически вызывает:

```http
GET /IndustryManagment/WebIntegration/GetFileUniversal?machine_id=18&file_type=0
```

Возможные ответы:

- `EOF` - файл закончился, выйти из цикла.
- `<base64-file-chunk>` - очередной блок файла. Его нужно декодировать из Base64 и дописать в конец локального файла.

Перед первым блоком локальный файл очищается/создается пустым.

### Шаг 3: проверка хеша

После `EOF`:

1. Декодировать ожидаемый хеш из Base64 в байты.
2. Прочитать полученный файл в байты.
3. Посчитать MD5 от полученного файла.
4. Сравнить байты MD5.
5. Текущий код только печатает результат:
   - `Hashes are equal`
   - `Hashes are different`

В C# реализации лучше вернуть результат проверки вызывающему коду или залогировать ошибку, если хеш не совпал.

## Полный алгоритм `update_data_for_dispatcher`

Одна итерация:

1. Прочитать `ServerAddress` и `ServerPort` из `DispatcherServerInfo`; если отсутствуют, использовать `127.0.0.1:8080`.
2. Сбросить флаг блокировки протокола в `false`.
3. Определить режим времени:
   - исторический режим, если включен `use_history_load` и есть отставание;
   - иначе актуальный режим.
4. Рассчитать `current_time_in_cycle`.
5. Инициализировать:
   - пустой `ListValues`;
   - путь основной УП = `null`;
   - путь подпрограммы = `null`.
6. Для каждого параметра из `ParametersList`:
   - вычислить значение по `Values` и `current_time_in_cycle`;
   - привести значение к `DataType`;
   - если значение непустое и `SpecialParameter = "PrgName"`, определить путь основной УП;
   - если значение непустое и `SpecialParameter = "Subprogram"`, определить путь подпрограммы;
   - добавить `{ "Key": parameter.Key, "Value": value }` в `ListValues`.
7. Сформировать тело `PostUniversalMonitoringDataJson`:

```json
{
  "MachineIntegrationId": 18,
  "UseInnerId": true,
  "ListValues": []
}
```

8. Отправить POST в Dispatcher.
9. Прочитать текст ответа.
10. По маркерам ответа выполнить дополнительные действия:
    - есть `FileType=1` -> `send_program(mainProgramPath)`;
    - есть `FileType=2` -> `send_program(subProgramPath)`;
    - есть `GetFile=1` -> `receive_program(received_program_machine_id_<id>.txt)`;
    - есть `FileType=0` и `GetFile=0` -> только логировать обычную отправку.
11. Если POST и дополнительные действия не упали с `ServerTimeout`/`ClientConnection`, обновить `last_date_sent = new_last_date_sent`.
12. Подождать:
    - `LoadHistoryInterval` в историческом режиме;
    - `SendInterval` в актуальном режиме.

## Ошибки и сетевое поведение

Текущая реализация обрабатывает только часть сетевых ошибок в `update_data_for_dispatcher`:

- `ServerTimeoutError`;
- `ClientConnectionError`.

При этих ошибках:

- печатается `Server is not reachable`;
- `last_date_sent` не обновляется;
- после обработки все равно выполняется ожидание `wait_interval`.

Отправка и получение УП не имеют полноценной проверки HTTP status code:

- ответ POST при отправке блока читается, но статус не проверяется;
- GET при получении файла читает текст, но статус не проверяется;
- несовпадение MD5 не прерывает программу, а только логируется.

Для C# реализации рекомендуется явно определить политику ошибок:

- считать ли не-2xx HTTP-статус ошибкой;
- сколько раз повторять отправку блока;
- удалять ли файл при несовпадении MD5;
- обновлять ли `lastDateSent`, если мониторинговые данные ушли, но обмен УП завершился ошибкой.

Если нужна максимальная совместимость с текущим Python-поведением, обновлять `lastDateSent` только когда весь блок `try` завершился без перехваченных сетевых исключений.

## Минимальные DTO для C#

```csharp
public sealed class UniversalPostRequest
{
    public object MachineIntegrationId { get; set; } = default!;
    public bool UseInnerId { get; set; }
    public List<UniversalValue> ListValues { get; set; } = new();
}

public sealed class UniversalValue
{
    public string Key { get; set; } = "";
    public object? Value { get; set; }
}
```

Для мониторинга `MachineIntegrationId` можно сериализовать числом, для отправки файла - строкой, если нужна полная совместимость с текущим клиентом.

## Псевдокод отправки файла

```text
SendProgram(path):
  if path is null:
    return

  bytes = ReadAllBytes(path)
  hashBase64 = Base64(MD5(bytes))
  offset = 0
  isFirstBlock = true

  while offset < bytes.Length:
    chunk = bytes[offset : offset + 4096]
    offset += chunk.Length
    eof = offset >= bytes.Length

    values = []
    if isFirstBlock:
      values.Add(Key="Hash", Value=hashBase64)
      isFirstBlock = false

    values.Add(Key="FileUP", Value=Base64(chunk))

    if eof:
      values.Add(Key="EOF", Value="1")

    POST PostFileUniversal {
      MachineIntegrationId = machineId as string,
      UseInnerId = useInnerId,
      ListValues = values
    }
```

## Псевдокод получения файла

```text
ReceiveProgram(path):
  answer = GET GetFileUniversal(machine_id, file_type=1)

  if answer does not contain "Hash=":
    return

  expectedHash = Base64Decode(answer.Substring(5))
  CreateOrTruncate(path)

  loop:
    chunkAnswer = GET GetFileUniversal(machine_id, file_type=0)

    if chunkAnswer == "EOF":
      break

    AppendBytes(path, Base64Decode(chunkAnswer))

  actualHash = MD5(ReadAllBytes(path))
  Compare expectedHash with actualHash
```

## Псевдокод `update_data_for_dispatcher`

```text
UpdateDataForDispatcher(currentTime, useHistoryLoad, loadHistoryInterval):
  if useHistoryLoad and lastDateSent < currentTime - 2 * sendInterval:
    sendTime = lastDateSent
    listValues.Add("MDCReadDate", Format(sendTime, "yyyy MM dd;HH:mm:ss"))
    newLastDateSent = lastDateSent + sendInterval
    waitInterval = loadHistoryInterval
  else:
    sendTime = currentTime
    newLastDateSent = currentTime
    waitInterval = sendInterval

  timeInCycle = (sendTime - (timeEmulatorStart % cycleLength)) % cycleLength
  mainProgramPath = null
  subProgramPath = null

  foreach parameter in parameters:
    value = CalculateValue(parameter.Values, timeInCycle, continueOnFormulaEnd)
    value = CastToDataType(value, parameter.DataType)

    if value is not null/empty/false:
      if parameter.SpecialParameter == "PrgName":
        mainProgramPath = ResolveProgramPath(value.ToString())
      if parameter.SpecialParameter == "Subprogram":
        subProgramPath = ResolveProgramPath(value.ToString())

    listValues.Add(parameter.Key, value)

  answer = POST PostUniversalMonitoringDataJson {
    MachineIntegrationId = protocolIntegrationId,
    UseInnerId = useInnerId,
    ListValues = listValues
  }

  if answer contains "FileType=1":
    SendProgram(mainProgramPath)

  if answer contains "FileType=2":
    SendProgram(subProgramPath)

  if answer contains "GetFile=1":
    ReceiveProgram(received_program_machine_id_<id>.txt)

  lastDateSent = newLastDateSent
  Delay(waitInterval)
```

## Совместимость с текущей реализацией

При переносе на C# важно сохранить следующие детали:

- Все файлы УП передаются байтами, а не строками.
- Хеш - именно MD5 digest в Base64, не hex-строка.
- Блоки файла - Base64 от исходных байтов блока.
- Размер блока отправки - `4096` байт.
- `Hash` отправляется только в первом блоке.
- `EOF = "1"` отправляется только в последнем блоке.
- При получении файла сначала запрашивается `file_type=1` для хеша, затем `file_type=0` для блоков.
- Завершение приема определяется строгим текстовым ответом `EOF`.
- Ответ `PostUniversalMonitoringDataJson` разбирается поиском подстрок.
- Полученные от Dispatcher программы имеют приоритет при последующем выборе файла: сначала `Received_Programs`, потом основной каталог программ.
