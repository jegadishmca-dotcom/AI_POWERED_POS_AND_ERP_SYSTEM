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

# Install native dependencies required by QuestPDF (SkiaSharp) and Npgsql
RUN apt-get update && apt-get install -y --no-install-recommends \
    libfontconfig1 \
    libssl3 \
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

# Health check
HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "PosErp.Api.dll"]
