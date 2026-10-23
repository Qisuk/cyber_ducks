# Build context: repository root (see compose.yaml).
# Stage 1 builds with the full SDK; stage 2 ships only the runtime, as a non-root user.
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:747cbade5bfd918a175ac32f8f1d931edec2c7d0d5397a3de72eaef1ac1f66d2 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/CyberDucks.Shared/CyberDucks.Shared.csproj src/CyberDucks.Shared/
COPY src/Store.Api/Store.Api.csproj src/Store.Api/
# Restore first, in its own layer, so package downloads are cached between code changes.
RUN dotnet restore src/Store.Api/Store.Api.csproj
COPY src/CyberDucks.Shared/ src/CyberDucks.Shared/
COPY src/Store.Api/ src/Store.Api/
RUN dotnet publish src/Store.Api/Store.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:8af87eba5e7cdafb1514d7388be287365da97c132509e84dfe252ffd552398ca
# Npgsql probes for Kerberos at startup; without this it logs a benign error. Keeps telemetry clean.
RUN apt-get update && apt-get install -y --no-install-recommends libgssapi-krb5-2 && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Store.Api.dll"]
