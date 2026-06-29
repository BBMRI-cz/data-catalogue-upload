# Build from the repo root as the context (central package management + project refs).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/BiobankApi/BiobankApi.Web/BiobankApi.Web.csproj
RUN dotnet publish src/BiobankApi/BiobankApi.Web/BiobankApi.Web.csproj \
    -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8001
# Runs the HTTP API (ingestion runs on the weekly Quartz schedule and via POST /admin/ingest).
ENTRYPOINT ["dotnet", "BiobankApi.Web.dll"]
