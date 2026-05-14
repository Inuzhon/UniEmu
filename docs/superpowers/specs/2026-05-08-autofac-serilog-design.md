# Autofac And Serilog Design

## Goal

Switch the backend from the default ASP.NET Core service provider to Autofac and replace default logging with Serilog configured from `appsettings`.

## Design

Use the minimal host-level integration:

- `Autofac.Extensions.DependencyInjection` supplies `AutofacServiceProviderFactory`.
- Existing `builder.Services` registrations stay in `Program.cs`.
- `Serilog.AspNetCore` installs Serilog as the host logger provider.
- Serilog reads sinks, output templates, log levels, file logging, and rotation settings from configuration.

This keeps the current small backend structure intact while leaving room to introduce Autofac modules later if registration volume grows.

## Configuration

`appsettings.json` owns the production defaults:

- console sink with configurable `outputTemplate`;
- file sink at `Logs/uniemu-.log`;
- daily rolling logs;
- size-based rolling at 10 MB;
- 14 retained files;
- category overrides for Microsoft and Quartz logs.

`appsettings.Development.json` only overrides levels so development can be more verbose without duplicating sink configuration.

## Verification

Build the backend project with:

```powershell
dotnet build UniEmu/UniEmu.csproj --ignore-failed-sources -p:BuildProjectReferences=false
```
