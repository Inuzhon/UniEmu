using UniEmu.DispatcherMock;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

var integration = app.MapGroup("/IndustryManagment/WebIntegration");

integration.MapPost("/PostUniversalMonitoringDataJson", () =>
    Results.Text(DispatcherMockResponses.MonitoringAccepted, "text/plain"));

integration.MapPost("/PostFileUniversal", () =>
    Results.Text(DispatcherMockResponses.ProgramUploadAccepted, "text/plain"));

integration.MapGet("/GetFileUniversal", (int file_type = 0) =>
{
    var answer = file_type == 1
        ? DispatcherMockResponses.EmptyProgramHash
        : DispatcherMockResponses.EndOfFile;

    return Results.Text(answer, "text/plain");
});

integration.MapGet("/GetIsMonitoringBlocked", () =>
    Results.Text(DispatcherMockResponses.MonitoringNotBlocked, "text/plain"));

await app.RunAsync();
