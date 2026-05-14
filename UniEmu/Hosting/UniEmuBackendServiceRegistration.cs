using Autofac;
using Microsoft.Extensions.DependencyInjection;
using UniEmu.Data;
using UniEmu.Features.CncPrograms;
using UniEmu.Features.Common;
using UniEmu.Features.Emulators;
using UniEmu.Features.Events;
using UniEmu.Features.Scripts;
using UniEmu.Features.Tags;
using UniEmu.Features.Telemetry;
using UniEmu.Realtime;
using UniEmu.Runtime;
using UniEmu.Runtime.Scripting;
using UniEmu.Runtime.Scripting.Environment;
using UniEmu.Runtime.Scripting.Rest;
using UniEmu.Runtime.Scripting.Services;
using UniEmu.Runtime.Scripting.Workspace;
using UniEmu.Scripting.Api;

namespace UniEmu.Hosting;

/// <summary>
/// Registers UniEmu application services in Autofac.
/// </summary>
public static class UniEmuBackendServiceRegistration
{
    public static void RegisterServices(ContainerBuilder container)
    {
        container.RegisterType<EmulatorService>().AsSelf().InstancePerLifetimeScope();
        container.RegisterType<DispatcherTemplateService>().AsSelf().InstancePerLifetimeScope();
        container.RegisterType<TagService>().AsSelf().InstancePerLifetimeScope();
        container.RegisterType<ScriptService>().AsSelf().InstancePerLifetimeScope();
        container.RegisterType<CncProgramService>().AsSelf().InstancePerLifetimeScope();
        container.RegisterType<EventService>().AsSelf().InstancePerLifetimeScope();
        container.RegisterType<TelemetryService>().AsSelf().InstancePerLifetimeScope();
        container.RegisterType<CachedUniEmuDataService>().AsSelf().InstancePerLifetimeScope();
        container.RegisterType<ScopedResourceValidator>().AsSelf().InstancePerLifetimeScope();
        container.RegisterType<RuntimeUpdateService>().AsSelf().InstancePerLifetimeScope();
        container.RegisterType<SignalRRuntimeUpdateBroadcaster>().As<IRuntimeUpdateBroadcaster>().InstancePerLifetimeScope();
        container.Register(context => new TagPreviewFlushService(
                context.Resolve<IServiceScopeFactory>(),
                context.Resolve<ILogger<TagPreviewFlushService>>()))
            .AsSelf()
            .SingleInstance();
        container.RegisterType<EmulatorScheduleService>().AsSelf().InstancePerLifetimeScope();
        container.Register(context => new TagScriptExecutionService(
                context.Resolve<UniEmuDbContext>(),
                context.Resolve<CachedUniEmuDataService>(),
                context.Resolve<TagRuntimeStateStore>(),
                context.Resolve<CompiledTagScriptCache>(),
                context.Resolve<CsxScriptEnvironment>(),
                context.Resolve<CsxScriptDirectiveValidator>(),
                context.Resolve<CsxScriptSecurityValidator>(),
                context.ResolveOptional<ITagScriptRestOperations>(),
                context.Resolve<TagPreviewFlushService>()))
            .AsSelf()
            .InstancePerLifetimeScope();
        container.RegisterType<TagRuntimeStatePersistenceService>().AsSelf().InstancePerLifetimeScope();

        container.RegisterType<TelemetryValueGenerator>().AsSelf().SingleInstance();
        container.RegisterType<TagRuntimeStateStore>().AsSelf().SingleInstance();
        container.RegisterType<CompiledTagScriptCache>().AsSelf().SingleInstance();
        container.RegisterType<CsxScriptEnvironment>().AsSelf().SingleInstance();
        container.RegisterType<CsxLoadedScriptExpander>().AsSelf().SingleInstance();
        container.RegisterType<CsxScriptDirectiveValidator>().AsSelf().SingleInstance();
        container.RegisterType<CsxScriptSecurityValidator>().AsSelf().SingleInstance();
        container.RegisterType<CsxRoslynContextFactory>().AsSelf().SingleInstance();
        container.RegisterType<CsxDiagnosticsService>().AsSelf().SingleInstance();
        container.RegisterType<CsxCompletionService>().AsSelf().SingleInstance();
        container.RegisterType<CsxHoverService>().AsSelf().SingleInstance();
        container.RegisterType<CsxSignatureHelpService>().AsSelf().SingleInstance();
        container.RegisterType<CsxLanguageService>().AsSelf().SingleInstance();
        container.RegisterType<CsxIntellisenseService>().AsSelf().InstancePerLifetimeScope();

        container.RegisterType<TelemetryPacketSender>().AsSelf().InstancePerDependency();
        container.RegisterType<AppSettingsRestCatalogProvider>().As<IRestCatalogProvider>().SingleInstance();
        container.RegisterType<TagScriptRestClient>().As<ITagScriptRestOperations>().InstancePerLifetimeScope();
    }
}
