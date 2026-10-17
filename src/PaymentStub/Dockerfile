# Build context: repository root (see compose.yaml).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/CyberDucks.Shared/CyberDucks.Shared.csproj src/CyberDucks.Shared/
COPY src/PaymentStub/PaymentStub.csproj src/PaymentStub/
RUN dotnet restore src/PaymentStub/PaymentStub.csproj
COPY src/CyberDucks.Shared/ src/CyberDucks.Shared/
COPY src/PaymentStub/ src/PaymentStub/
RUN dotnet publish src/PaymentStub/PaymentStub.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "PaymentStub.dll"]
