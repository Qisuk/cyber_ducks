# Build context: repository root (see compose.yaml).
# Stage 1 builds with the full SDK; stage 2 ships only the runtime, as a non-root user.
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:747cbade5bfd918a175ac32f8f1d931edec2c7d0d5397a3de72eaef1ac1f66d2 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/CyberDucks.Shared/CyberDucks.Shared.csproj src/CyberDucks.Shared/
COPY src/Store.Web/Store.Web.csproj src/Store.Web/
# Restore first, in its own layer, so package downloads are cached between code changes.
RUN dotnet restore src/Store.Web/Store.Web.csproj
COPY src/CyberDucks.Shared/ src/CyberDucks.Shared/
COPY src/Store.Web/ src/Store.Web/
RUN dotnet publish src/Store.Web/Store.Web.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:8af87eba5e7cdafb1514d7388be287365da97c132509e84dfe252ffd552398ca
# Npgsql probes for Kerberos at startup; without this it logs a benign error. Keeps telemetry clean.
RUN apt-get update && apt-get install -y --no-install-recommends libgssapi-krb5-2 && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
# Data Protection keys volume: create it owned by the non-root app user. A fresh named volume
# mounted here inherits this ownership, so the app can write its keys.
RUN mkdir -p /keys && chown $APP_UID /keys
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Store.Web.dll"]
