# =============================================================
# Enterprise Supermarket POS & ERP System — Backend Dockerfile
# Multi-stage build for ASP.NET Core 8 (PosErp.Api)
# =============================================================

# ── Stage 1: Restore ─────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS restore
WORKDIR /src

# Copy solution and all project files first (layer caching)
COPY PosErp.sln ./
COPY PosErp.Domain/PosErp.Domain.csproj           PosErp.Domain/
COPY PosErp.Application/PosErp.Application.csproj  PosErp.Application/
COPY PosErp.Infrastructure/PosErp.Infrastructure.csproj PosErp.Infrastructure/
COPY PosErp.Api/PosErp.Api.csproj                  PosErp.Api/

# Restore NuGet packages for API and its dependencies only
# (PosErp.sln also references IntegrationTests which is not in the Docker build context)
RUN dotnet restore PosErp.Api/PosErp.Api.csproj

# ── Stage 2: Build & Publish ─────────────────────────────────
FROM restore AS publish
WORKDIR /src

# Copy full source code
COPY . .

# Publish Release build — single-layer output
RUN dotnet publish PosErp.Api/PosErp.Api.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false \
    --no-restore

# ── Stage 3: Runtime ─────────────────────────────────────────
# Use standard Debian-based runtime (not Alpine) — required for
# QuestPDF/SkiaSharp native libs (libstdc++, libgcc, icu-libs)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Install native dependencies required by QuestPDF (SkiaSharp), Npgsql, and health check.
# postgresql-client-16 is pinned to match the Postgres server version.
# It provides pg_dump and pg_restore required by RefreshUatFromLiveSnapshotAsync.
# postgresql-client-16 is NOT in Debian Bookworm's default repos — we must add the
# official PostgreSQL PGDG apt repository first.
# If you upgrade Postgres, update the client version number in both places below.
RUN apt-get update && apt-get install -y --no-install-recommends \
    gnupg \
    curl \
    lsb-release \
    ca-certificates \
    && curl -fsSL https://www.postgresql.org/media/keys/ACCC4CF8.asc \
       | gpg --dearmor -o /usr/share/keyrings/postgresql-archive-keyring.gpg \
    && echo "deb [signed-by=/usr/share/keyrings/postgresql-archive-keyring.gpg] \
       https://apt.postgresql.org/pub/repos/apt $(lsb_release -cs)-pgdg main" \
       > /etc/apt/sources.list.d/postgresql.list \
    && apt-get update \
    && apt-get install -y --no-install-recommends \
       libfontconfig1 \
       libssl3 \
       postgresql-client-16 \
    && rm -rf /var/lib/apt/lists/*

# Security: run as non-root
RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser
USER appuser

# Copy published output
COPY --from=publish /app/publish .

# ASP.NET Core 8 defaults to port 8080 in containers
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_EnableDiagnostics=0

EXPOSE 8080

# Health check (use curl — wget is not available in Debian aspnet image)
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "PosErp.Api.dll"]
