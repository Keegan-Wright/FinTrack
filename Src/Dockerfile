FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Directory.Packages.props", "Directory.Build.props", ".editorconfig", "global.json", "./"]
COPY ["Src/", "Src/"]

RUN dotnet restore "Src/FinanceTracker/FinanceTracker.csproj"

# Publish builds the app itself
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
# The release's git tag (e.g. v0.1.1) and commit, shown in the app. .git isn't copied in, so the commit is passed
ARG VERSION=v0.0.0-dev
ARG SOURCE_REVISION=
RUN dotnet publish "Src/FinanceTracker/FinanceTracker.csproj" -c $BUILD_CONFIGURATION --no-restore \
    -o /app/publish /p:UseAppHost=false /p:Version=${VERSION#v} /p:SourceRevisionId=$SOURCE_REVISION

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FinanceTracker.dll"]
