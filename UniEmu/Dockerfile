FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 5083

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl ca-certificates gnupg \
    && curl -fsSL https://deb.nodesource.com/setup_20.x | bash - \
    && apt-get install -y --no-install-recommends nodejs \
    && npm install -g yarn@1.22.22 \
    && rm -rf /var/lib/apt/lists/*

COPY ["Directory.Build.props", "."]
COPY ["NuGet.Config", "."]
COPY ["UniEmu/UniEmu.csproj", "UniEmu/"]
COPY ["UniEmu.Client/UniEmu.Client.esproj", "UniEmu.Client/"]
RUN dotnet restore "UniEmu/UniEmu.csproj"

COPY . .
WORKDIR "/src/UniEmu"
RUN dotnet publish "UniEmu.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "UniEmu.dll"]
