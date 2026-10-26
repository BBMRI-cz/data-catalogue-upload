# Build from the repo root as the context (central package management + project refs).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/Uploader/Uploader.Host/Uploader.Host.csproj
RUN dotnet publish src/Uploader/Uploader.Host/Uploader.Host.csproj \
    -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8003
# Runs the sync service (syncs on the Mon/Wed/Fri Quartz schedule and via POST /admin/sync).
ENTRYPOINT ["dotnet", "Uploader.Host.dll"]
