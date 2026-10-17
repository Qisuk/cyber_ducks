# Build context: repository root (see compose.yaml).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/CyberDucks.Shared/CyberDucks.Shared.csproj src/CyberDucks.Shared/
COPY src/RangeControl/RangeControl.csproj src/RangeControl/
RUN dotnet restore src/RangeControl/RangeControl.csproj
COPY src/CyberDucks.Shared/ src/CyberDucks.Shared/
COPY src/RangeControl/ src/RangeControl/
# Note: no --no-restore. The interactive Blazor framework assets (_framework/blazor.web.js) are
# only emitted when publish runs its own restore/build here; --no-restore drops them.
RUN dotnet publish src/RangeControl/RangeControl.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
# Npgsql probes for Kerberos at startup; without this it logs a benign error. Keeps telemetry clean.
RUN apt-get update && apt-get install -y --no-install-recommends libgssapi-krb5-2 && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
RUN mkdir -p /keys && chown $APP_UID /keys
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "RangeControl.dll"]
