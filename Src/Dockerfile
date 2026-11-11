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
COPY ["Src/FinanceTracker.Emails/FinanceTracker.Emails.csproj", "Src/FinanceTracker.Emails/"]
COPY ["Src/FinanceTracker.Infrastructure/FinanceTracker.Infrastructure.csproj", "Src/FinanceTracker.Infrastructure/"]
COPY ["Src/FinanceTracker.Infrastructure.Migrations/FinanceTracker.Infrastructure.Migrations.csproj", "Src/FinanceTracker.Infrastructure.Migrations/"]
COPY ["Src/FinanceTracker.Telemetry/FinanceTracker.Telemetry.csproj", "Src/FinanceTracker.Telemetry/"]
COPY ["Src/Generators/FinanceTracker.Generated.Attributes/FinanceTracker.Generated.Attributes/FinanceTracker.Generated.Attributes.csproj", "Src/Generators/FinanceTracker.Generated.Attributes/FinanceTracker.Generated.Attributes/"]
COPY ["Src/Generators/FinanceTracker.Generated.DependencyInjection/FinanceTracker.Generated.DependencyInjection/FinanceTracker.Generated.DependencyInjection.csproj", "Src/Generators/FinanceTracker.Generated.DependencyInjection/FinanceTracker.Generated.DependencyInjection/"]
COPY ["Src/Generators/FinanceTracker.Generated.Enums/FinanceTracker.Generated.Enums/FinanceTracker.Generated.Enums.csproj", "Src/Generators/FinanceTracker.Generated.Enums/FinanceTracker.Generated.Enums/"]
COPY ["Src/Generators/FinanceTracker.Generated.Shared/FinanceTracker.Generated.Shared.csproj", "Src/Generators/FinanceTracker.Generated.Shared/"]
RUN dotnet restore "Src/FinanceTracker/FinanceTracker.csproj" -a $TARGETARCH

COPY ["Src/", "Src/"]

# Pyroscope's .NET profiler, for continuous profiling. It only loads when the compose file turns it on with
# CORECLR_ENABLE_PROFILING and the rest of its settings, otherwise the files sit unused. Pinned by checksum, so a
# release replaced upstream fails the build rather than going into the image
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS profiler
ARG TARGETARCH
ARG PYROSCOPE_VERSION=1.6.1
ARG PYROSCOPE_SHA256_ARM64=de0f1fc384f2af754bfb331ce4c0028ba92ec6a25ce98efba4aa9e2f81a012c4
ARG PYROSCOPE_SHA256_AMD64=9efda5cf621511ff937a0f765fe8af0ea59ab8c47a227c8afcf25637779dd36f
RUN if [ "$TARGETARCH" = "arm64" ]; then \
        arch=aarch64; sha256=$PYROSCOPE_SHA256_ARM64; \
    else \
        arch=x86_64; sha256=$PYROSCOPE_SHA256_AMD64; \
    fi \
    && curl -fsSL -o /tmp/pyroscope.tar.gz \
        "https://github.com/grafana/pyroscope-dotnet/releases/download/pyroscope-${PYROSCOPE_VERSION}/pyroscope.${PYROSCOPE_VERSION}-glibc-${arch}.tar.gz" \
    && echo "$sha256  /tmp/pyroscope.tar.gz" | sha256sum -c - \
    && mkdir /pyroscope \
    && tar xzf /tmp/pyroscope.tar.gz -C /pyroscope

# The profiler needs zlib, which .NET's own images stopped carrying in .NET 9 (the runtime has its own copy built in).
# Without it the runtime quietly skips loading the profiler. Taken from the same Ubuntu release the chiseled image is
# cut from, for the target's architecture, and put beside the profiler, which looks in its own folder first. Nothing
# runs in this stage, so it builds without emulation
FROM ubuntu:24.04 AS zlib

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
COPY --from=profiler /pyroscope /pyroscope
COPY --from=zlib /usr/lib/*-linux-gnu/libz.so.1* /pyroscope/
ENTRYPOINT ["dotnet", "FinanceTracker.dll"]
