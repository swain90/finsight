# ─── Stage 1: Build ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files first for layer caching
COPY ["src/FinSight.Api/FinSight.Api.csproj", "src/FinSight.Api/"]
COPY ["src/FinSight.Application/FinSight.Application.csproj", "src/FinSight.Application/"]
COPY ["src/FinSight.Domain/FinSight.Domain.csproj", "src/FinSight.Domain/"]
COPY ["src/FinSight.Infrastructure/FinSight.Infrastructure.csproj", "src/FinSight.Infrastructure/"]
COPY ["Directory.Packages.props", "."]

# Restore with cache mount
RUN --mount=type=cache,id=finsight-nuget,target=/root/.nuget/packages \
    dotnet restore "src/FinSight.Api/FinSight.Api.csproj"

# Copy and build
COPY . .
WORKDIR /src/src/FinSight.Api
RUN --mount=type=cache,id=finsight-nuget,target=/root/.nuget/packages \
    dotnet build "FinSight.Api.csproj" -c Release

# ─── Stage 2: Publish ──────────────────────────────────────────────────────────
FROM build AS publish
RUN --mount=type=cache,id=finsight-nuget,target=/root/.nuget/packages \
    dotnet publish "FinSight.Api.csproj" -c Release --no-build -o /app/publish \
    /p:UseAppHost=false

# ─── Stage 3: Runtime ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final
WORKDIR /app

# Required when invariant globalization is disabled.
RUN apk add --no-cache icu-libs

# Security: non-root user (built into .NET 8 images)
USER $APP_UID

# Performance: configure GC for containers
ENV DOTNET_GCConserveMemory=9
ENV DOTNET_GCHighMemPercent=75

# Health check
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:8080/health/live || exit 1

EXPOSE 8080

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FinSight.Api.dll"]
