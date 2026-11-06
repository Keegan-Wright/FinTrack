FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra AS base
WORKDIR /app
EXPOSE 8080


FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src
COPY ["Directory.Packages.props", "Directory.Build.props", ".editorconfig", "global.json", "./"]

COPY ["Src/FinanceTracker/FinanceTracker.csproj", "Src/FinanceTracker/"]
COPY ["Src/FinanceTracker.AppHost.ServiceDefaults/FinanceTracker.AppHost.ServiceDefaults.csproj", "Src/FinanceTracker.AppHost.ServiceDefaults/"]
COPY ["Src/FinanceTracker.Client/FinanceTracker.Client.csproj", "Src/FinanceTracker.Client/"]
COPY ["Src/FinanceTracker.Contracts/FinanceTracker.Contracts.csproj", "Src/FinanceTracker.Contracts/"]
COPY ["Src/FinanceTracker.Design/FinanceTracker.Design.csproj", "Src/FinanceTracker.Design/"]
COPY ["Src/FinanceTracker.Domain/FinanceTracker.Domain.csproj", "Src/FinanceTracker.Domain/"]
COPY ["Src/FinanceTracker.Infrastructure/FinanceTracker.Infrastructure.csproj", "Src/FinanceTracker.Infrastructure/"]
COPY ["Src/FinanceTracker.Infrastructure.Migrations/FinanceTracker.Infrastructure.Migrations.csproj", "Src/FinanceTracker.Infrastructure.Migrations/"]
COPY ["Src/Generators/FinanceTracker.Generated.Attributes/FinanceTracker.Generated.Attributes/FinanceTracker.Generated.Attributes.csproj", "Src/Generators/FinanceTracker.Generated.Attributes/FinanceTracker.Generated.Attributes/"]
COPY ["Src/Generators/FinanceTracker.Generated.DependencyInjection/FinanceTracker.Generated.DependencyInjection/FinanceTracker.Generated.DependencyInjection.csproj", "Src/Generators/FinanceTracker.Generated.DependencyInjection/FinanceTracker.Generated.DependencyInjection/"]
COPY ["Src/Generators/FinanceTracker.Generated.Enums/FinanceTracker.Generated.Enums/FinanceTracker.Generated.Enums.csproj", "Src/Generators/FinanceTracker.Generated.Enums/FinanceTracker.Generated.Enums/"]
COPY ["Src/Generators/FinanceTracker.Generated.Shared/FinanceTracker.Generated.Shared.csproj", "Src/Generators/FinanceTracker.Generated.Shared/"]
RUN dotnet restore "Src/FinanceTracker/FinanceTracker.csproj" -a $TARGETARCH

COPY ["Src/", "Src/"]

# Publish builds the app itself, for the target's architecture only, so the image carries only its native libraries
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
# The release's git tag (e.g. v0.1.1) and commit, shown in the app. .git isn't copied in, so the commit is passed
ARG VERSION=v0.0.0-dev
ARG SOURCE_REVISION=
RUN dotnet publish "Src/FinanceTracker/FinanceTracker.csproj" -c $BUILD_CONFIGURATION -a $TARGETARCH --no-restore \
    -o /app/publish /p:UseAppHost=false /p:Version=${VERSION#v} /p:SourceRevisionId=$SOURCE_REVISION

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FinanceTracker.dll"]
