FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["Directory.Build.props", "."]
COPY ["UniEmu.DispatcherMock/UniEmu.DispatcherMock.csproj", "UniEmu.DispatcherMock/"]
RUN dotnet restore "UniEmu.DispatcherMock/UniEmu.DispatcherMock.csproj"

COPY ["UniEmu.DispatcherMock/", "UniEmu.DispatcherMock/"]
WORKDIR /src/UniEmu.DispatcherMock
RUN dotnet publish "UniEmu.DispatcherMock.csproj" \
    -c "$BUILD_CONFIGURATION" \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "UniEmu.DispatcherMock.dll"]
