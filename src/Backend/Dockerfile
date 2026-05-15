# =============================================================
# Enterprise Supermarket POS & ERP System — Backend Dockerfile
# Multi-stage build for ASP.NET Core 8 (PosErp.Api)
# =============================================================

# ── Stage 1: Restore ─────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS restore
WORKDIR /src

# Copy solution and all project files first (layer caching)
COPY PosErp.sln ./
COPY PosErp.Domain/PosErp.Domain.csproj           PosErp.Domain/
COPY PosErp.Application/PosErp.Application.csproj  PosErp.Application/
COPY PosErp.Infrastructure/PosErp.Infrastructure.csproj PosErp.Infrastructure/
COPY PosErp.Api/PosErp.Api.csproj                  PosErp.Api/

# Restore NuGet packages (cached unless .csproj files change)
RUN dotnet restore PosErp.sln

# ── Stage 2: Build & Publish ─────────────────────────────────
FROM restore AS publish
WORKDIR /src

# Copy full source code
COPY . .

# Publish Release build — single-layer, trimmed output
RUN dotnet publish PosErp.Api/PosErp.Api.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false \
    --no-restore

# ── Stage 3: Runtime ─────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final
WORKDIR /app

# Security: run as non-root
RUN addgroup -S appgroup && adduser -S appuser -G appgroup
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
