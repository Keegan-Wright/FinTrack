FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Directory.Packages.props", "Directory.Build.props", ".editorconfig", "global.json", "./"]
COPY ["Src/", "Src/"]

RUN dotnet restore "Src/FinanceTracker/FinanceTracker.csproj"
RUN dotnet build "Src/FinanceTracker/FinanceTracker.csproj" -c $BUILD_CONFIGURATION --no-restore

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Src/FinanceTracker/FinanceTracker.csproj" -c $BUILD_CONFIGURATION --no-restore \
    -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FinanceTracker.dll"]
