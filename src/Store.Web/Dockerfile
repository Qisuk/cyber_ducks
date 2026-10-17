# Build context: repository root (see compose.yaml).
# Stage 1 builds with the full SDK; stage 2 ships only the runtime, as a non-root user.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/CyberDucks.Shared/CyberDucks.Shared.csproj src/CyberDucks.Shared/
COPY src/Store.Web/Store.Web.csproj src/Store.Web/
# Restore first, in its own layer, so package downloads are cached between code changes.
RUN dotnet restore src/Store.Web/Store.Web.csproj
COPY src/CyberDucks.Shared/ src/CyberDucks.Shared/
COPY src/Store.Web/ src/Store.Web/
RUN dotnet publish src/Store.Web/Store.Web.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Store.Web.dll"]
