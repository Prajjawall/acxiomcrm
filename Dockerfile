# Stage 1: Build client assets (Node 22)
FROM node:22-bookworm-slim AS node-build
WORKDIR /app
COPY src/AcxiomCrm.Web/ ./src/AcxiomCrm.Web/
RUN if [ -f src/AcxiomCrm.Web/package.json ]; then \
      cd src/AcxiomCrm.Web && npm ci && npm run build; \
    else \
      echo "No package.json yet, skipping asset build"; \
    fi

# Stage 2: Build .NET application
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["global.json", "Directory.Build.props", "Directory.Packages.props", "AcxiomCrm.sln", ".editorconfig", "BannedSymbols.txt", "./"]
COPY ["src/AcxiomCrm.Domain/AcxiomCrm.Domain.csproj", "src/AcxiomCrm.Domain/"]
COPY ["src/AcxiomCrm.Application/AcxiomCrm.Application.csproj", "src/AcxiomCrm.Application/"]
COPY ["src/AcxiomCrm.Infrastructure/AcxiomCrm.Infrastructure.csproj", "src/AcxiomCrm.Infrastructure/"]
COPY ["src/AcxiomCrm.Web/AcxiomCrm.Web.csproj", "src/AcxiomCrm.Web/"]

RUN dotnet restore "src/AcxiomCrm.Web/AcxiomCrm.Web.csproj" || \
    (sleep 2 && dotnet restore "src/AcxiomCrm.Web/AcxiomCrm.Web.csproj" --disable-parallel) || \
    dotnet restore "src/AcxiomCrm.Web/AcxiomCrm.Web.csproj" --disable-parallel

COPY src/ src/
RUN dotnet publish "src/AcxiomCrm.Web/AcxiomCrm.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

USER app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

HEALTHCHECK --interval=10s --timeout=3s --retries=3 --start-period=5s \
  CMD curl -f http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "AcxiomCrm.Web.dll"]
