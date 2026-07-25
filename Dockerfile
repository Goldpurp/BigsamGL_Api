FROM mcr.microsoft.com/dotnet/sdk:8.0.423 AS build
WORKDIR /src

COPY LogisticsSystem.sln Directory.Build.props global.json ./
COPY .config/ .config/
COPY Domain/Domain.csproj Domain/
COPY Application/Application.csproj Application/
COPY Infrastructure/Infrastructure.csproj Infrastructure/
COPY Api/Api.csproj Api/
RUN dotnet restore Api/Api.csproj

COPY . .
RUN dotnet tool restore
RUN dotnet publish Api/Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false
RUN dotnet tool run dotnet-ef migrations bundle \
    --project Infrastructure/Infrastructure.csproj \
    --startup-project Api/Api.csproj \
    --configuration Release \
    --no-build \
    --output /app/migrate

FROM mcr.microsoft.com/dotnet/aspnet:8.0.29 AS runtime
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

COPY --from=build --chown=app:app /app/publish .
COPY --from=build --chown=app:app /app/migrate ./migrate
RUN mkdir -p /var/data/bigsamgl-keys && chown -R app:app /var/data

USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "Api.dll"]
