# Build context: repository root (see compose.yaml).
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:747cbade5bfd918a175ac32f8f1d931edec2c7d0d5397a3de72eaef1ac1f66d2 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/CyberDucks.Shared/CyberDucks.Shared.csproj src/CyberDucks.Shared/
COPY src/PaymentStub/PaymentStub.csproj src/PaymentStub/
RUN dotnet restore src/PaymentStub/PaymentStub.csproj
COPY src/CyberDucks.Shared/ src/CyberDucks.Shared/
COPY src/PaymentStub/ src/PaymentStub/
RUN dotnet publish src/PaymentStub/PaymentStub.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:8af87eba5e7cdafb1514d7388be287365da97c132509e84dfe252ffd552398ca
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "PaymentStub.dll"]
