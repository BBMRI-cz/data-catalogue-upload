# Build from the repo root as the context (central package management + project refs).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/SequencingApi/SequencingApi.Web/SequencingApi.Web.csproj
RUN dotnet publish src/SequencingApi/SequencingApi.Web/SequencingApi.Web.csproj \
    -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8002
# Runs the HTTP API (ingestion runs on the weekly Quartz schedule and via POST /admin/ingest).
ENTRYPOINT ["dotnet", "SequencingApi.Web.dll"]
