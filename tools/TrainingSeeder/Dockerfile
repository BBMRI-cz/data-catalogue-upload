# The training seeder (#112). Built by compose.training.yml only; no production compose file uses it.
# Build from the repo root as the context (central package management + project refs).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore tools/TrainingSeeder/TrainingSeeder.csproj
RUN dotnet publish tools/TrainingSeeder/TrainingSeeder.csproj \
    -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
# Fills two empty databases once and exits; see compose.training.yml.
ENTRYPOINT ["dotnet", "TrainingSeeder.dll"]
