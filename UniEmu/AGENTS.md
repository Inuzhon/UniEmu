# UniEmu Backend Agent Guide

## Документация

- Документация проекта и документация в коде должны быть на русском языке.
- XML-комментарии C# (`summary`, `param`, `returns` и т.д.) пишите по-русски, включая комментарии для приватных методов и вспомогательных типов.

Этот документ описывает нюансы работы с backend-проектом `UniEmu`.

## Кодировка файлов

- Backend-файлы хранятся в UTF-8 with BOM. При чтении через PowerShell указывайте кодировку явно, например `Get-Content -Raw -Encoding UTF8 UniEmu\Runtime\Scripting\CsxLanguageService.cs`, чтобы не получить mojibake.
- При изменении существующих backend-файлов сохраняйте исходную кодировку UTF-8 with BOM и не переписывайте файл инструментами, которые удаляют BOM или интерпретируют UTF-8 как ANSI/OEM.

## Сборка и тесты

- Backend использует .NET 10 preview, поэтому предупреждение `NETSDK1057` про preview SDK само по себе не является ошибкой.
- Если `dotnet build`, `dotnet publish` или `dotnet test` из CLI зависает без понятного вывода, сначала сбросьте фоновые build-серверы:

```powershell
dotnet build-server shutdown
```

- Для повторного запуска из CLI предпочитайте одноразовый режим без MSBuild server, node reuse и shared compiler:

```powershell
$env:DOTNET_CLI_USE_MSBUILD_SERVER='0'
dotnet test UniEmu.Tests\UniEmu.Tests.csproj --no-restore -nr:false -p:UseSharedCompilation=false -p:SkipYarnBuild=True
```

- Для проверки компиляции без тестов используйте похожий формат:

```powershell
$env:DOTNET_CLI_USE_MSBUILD_SERVER='0'
dotnet build UniEmu.Tests\UniEmu.Tests.csproj --no-restore -nr:false -p:UseSharedCompilation=false -p:SkipYarnBuild=True /v:minimal
```

- Желательно не запускать параллельно сборку проекта и запуск тестов. В этом репозитории параллельные `dotnet build`/`dotnet test` могут цепляться за одни и те же MSBuild/Roslyn build servers и подвисать без полезной диагностики.
- Если Visual Studio открыта и активно собирает проект в фоне, CLI-проверки лучше запускать после завершения фоновой сборки или через команды выше.

## Backend port

- Порт backend задается настройкой `UniEmu:Port`.
- Для Docker Compose используется переменная `UNIEMU_PORT`, которая передается в контейнер как `UniEmu__Port`.
- При старте приложение логирует выбранный порт сообщением `UniEmu backend listening on port {Port}`.
