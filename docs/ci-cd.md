# UniEmu CI/CD

Этот документ описывает текущую схему сборки, Docker packaging и GitLab pipeline.

## Цели

- На каждый push запускать сборку backend, frontend и backend-тесты.
- Собирать единый Docker image с ASP.NET Core backend и статикой frontend внутри `wwwroot`.
- Публиковать Docker image в registry.
- Собирать Windows publish output `win-x64`, упаковывать его в zip и сохранять как GitLab artifact.

## Docker image

Основной Dockerfile находится в `UniEmu/Dockerfile`.

Сборка разделена на stages:

- `client-build` использует `node:20-bookworm-slim`, ставит зависимости через `yarn install --frozen-lockfile` и выполняет `yarn build`.
- `backend-build` использует `mcr.microsoft.com/dotnet/sdk:10.0`, восстанавливает и публикует `UniEmu.csproj`.
- Перед publish frontend `dist` копируется в `UniEmu/wwwroot`, а `dotnet publish` запускается с `-p:SkipYarnBuild=True`.
- `final` использует `mcr.microsoft.com/dotnet/aspnet:10.0` и запускает `UniEmu.dll`.

Решение с явным frontend stage выбрано, чтобы Docker image не зависел от локального `dist` и всегда собирал актуальную статику внутри контейнерной сборки.

## Docker Compose

Корневой `docker-compose.yml` поднимает один сервис `uniemu`.

Настройки:

- image по умолчанию: `uniemu:local`, можно переопределить через `UNIEMU_IMAGE`.
- внешний порт: `${UNIEMU_PORT:-5083}`.
- внутренний порт приложения: `5083`, передается через `UniEmu__Port`.
- SQLite база хранится в volume `uniemu-data` по пути `/app/data/uniemu.db`.
- логи хранятся в volume `uniemu-logs` по пути `/app/Logs`.

Пример запуска:

```powershell
docker compose up -d --build
```

## GitLab pipeline

Корневой `.gitlab-ci.yml` содержит stages:

- `test`
- `package`
- `publish`

`build_and_test`:

- использует `.NET 10 SDK`;
- устанавливает Node.js 20 и Yarn 1.22.22;
- выполняет `dotnet restore`;
- выполняет `dotnet build` для `UniEmu.Tests.csproj`;
- выполняет `dotnet test`;
- выполняет `yarn install --frozen-lockfile` и `yarn build` для frontend;
- сохраняет `TestResults/` как artifact.

`windows_archive`:

- собирает frontend;
- копирует `UniEmu.Client/dist` в `UniEmu/wwwroot`;
- выполняет `dotnet publish UniEmu/UniEmu.csproj -r win-x64 --self-contained false`;
- упаковывает результат в `artifacts/UniEmu-win-x64-<commit>.zip`;
- публикует zip как GitLab artifact.

`docker_image`:

- использует Docker-in-Docker;
- собирает image из `UniEmu/Dockerfile`;
- пушит tags `<commit-short-sha>` и `<branch-slug>`;
- на default branch дополнительно пушит `latest`.

## Registry variables

По умолчанию pipeline использует встроенный GitLab registry:

- `CI_REGISTRY`
- `CI_REGISTRY_IMAGE`
- `CI_REGISTRY_USER`
- `CI_REGISTRY_PASSWORD`

Для локального registry можно задать переменные GitLab CI/CD:

- `LOCAL_DOCKER_REGISTRY`, например `registry.local:5000`.
- `LOCAL_DOCKER_REGISTRY_IMAGE`, например `registry.local:5000/uniemu/uniemu`.
- `LOCAL_DOCKER_REGISTRY_USER`, если registry требует логин.
- `LOCAL_DOCKER_REGISTRY_PASSWORD`, если registry требует пароль.

Если логин и пароль не заданы, job попробует пушить без `docker login`. Это удобно для локального insecure/private registry, где авторизация отключена или настроена на уровне runner.

## Важные решения

- GitLab читает pipeline из корневого `.gitlab-ci.yml`; старый файл `.gitlab/workflows/.gitlab-ci.yml` оставлен нетронутым как исторический template.
- `SkipYarnBuild=True` теперь отключает `ProjectReference` на `UniEmu.Client.esproj`, чтобы Docker/CI publish не запускал frontend второй раз через MSBuild.
- Frontend static assets попадают в publish output через `UniEmu/wwwroot`, поэтому backend отдает SPA через существующий `UseStaticFiles()` и `MapFallbackToFile("/index.html")`.
- Windows artifact делается framework-dependent (`--self-contained false`), поэтому на целевой Windows-машине нужен установленный .NET runtime совместимой версии.
