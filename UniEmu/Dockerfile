FROM node:20-bookworm-slim AS client-build
WORKDIR /src/UniEmu.Client

COPY ["UniEmu.Client/package.json", "UniEmu.Client/yarn.lock", "./"]
RUN yarn install --frozen-lockfile

COPY ["UniEmu.Client/", "./"]
RUN yarn build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend-build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["Directory.Build.props", "."]
COPY ["NuGet.Config", "."]
COPY ["UniEmu/UniEmu.csproj", "UniEmu/"]
COPY ["UniEmu.Scripting.Api/UniEmu.Scripting.Api.csproj", "UniEmu.Scripting.Api/"]
COPY ["UniEmu.Client/UniEmu.Client.esproj", "UniEmu.Client/"]
RUN dotnet restore "UniEmu/UniEmu.csproj"

COPY . .
RUN rm -rf "UniEmu/wwwroot" \
    && mkdir -p "UniEmu/wwwroot"
COPY --from=client-build /src/UniEmu.Client/dist/ UniEmu/wwwroot/

WORKDIR /src/UniEmu
RUN dotnet publish "UniEmu.csproj" \
    -c "$BUILD_CONFIGURATION" \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false \
    /p:SkipYarnBuild=True

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    UniEmu__Port=5083 \
    ConnectionStrings__UniEmuDb="Data Source=/app/data/uniemu.db"

EXPOSE 5083

COPY --from=backend-build /app/publish .

RUN mkdir -p /app/data /app/Logs

ENTRYPOINT ["dotnet", "UniEmu.dll"]
