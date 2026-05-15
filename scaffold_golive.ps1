$backendDir = "d:\JEGADISH\AI_POWERED_POS_AND_ERP_SYSTEM\src\Backend"
$infraDir = "d:\JEGADISH\AI_POWERED_POS_AND_ERP_SYSTEM\infrastructure"
$rootDir = "d:\JEGADISH\AI_POWERED_POS_AND_ERP_SYSTEM"

New-Item -ItemType Directory -Force -Path "$backendDir\PosErp.Api\Middlewares"
New-Item -ItemType Directory -Force -Path $infraDir

# 1. Global Exception Middleware
@"
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

namespace PosErp.Api.Middlewares;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext httpContext)
    {
        try
        {
            await _next(httpContext);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred.");
            await HandleExceptionAsync(httpContext, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        var response = new
        {
            StatusCode = context.Response.StatusCode,
            Message = "Internal Server Error from the custom middleware.",
            Detailed = exception.Message // In production, hide stack trace and details
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}
"@ | Out-File -FilePath "$backendDir\PosErp.Api\Middlewares\GlobalExceptionMiddleware.cs" -Encoding utf8

# 2. Audit Logs Schema
@"
-- ==============================================================================
-- PHASE 6: AUDIT LOGS SCHEMA
-- ==============================================================================

CREATE TABLE audit_logs (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID, -- Can be null for system actions
    action VARCHAR(100) NOT NULL, -- e.g., 'PRICE_OVERRIDE', 'STOCK_ADJUSTMENT'
    entity_name VARCHAR(100),
    entity_id VARCHAR(100),
    old_values JSONB,
    new_values JSONB,
    ip_address VARCHAR(45),
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_audit_logs_action ON audit_logs(action);
CREATE INDEX idx_audit_logs_created_at ON audit_logs(created_at);
"@ | Out-File -FilePath "$backendDir\PosErp.Infrastructure\Persistence\Migrations\14_AuditLogsSchema.sql" -Encoding utf8

# 3. Docker Compose Production
@"
version: '3.8'

services:
  db:
    image: postgres:15-alpine
    environment:
      POSTGRES_USER: \${DB_USER}
      POSTGRES_PASSWORD: \${DB_PASSWORD}
      POSTGRES_DB: \${DB_NAME}
    volumes:
      - pgdata:/var/lib/postgresql/data
    networks:
      - erp-net
    restart: unless-stopped

  pgbouncer:
    image: edoburu/pgbouncer:latest
    environment:
      DB_USER: \${DB_USER}
      DB_PASSWORD: \${DB_PASSWORD}
      DB_HOST: db
      DB_NAME: \${DB_NAME}
      POOL_MODE: transaction
      MAX_CLIENT_CONN: 1000
      DEFAULT_POOL_SIZE: 50
    ports:
      - "6432:5432"
    depends_on:
      - db
    networks:
      - erp-net
    restart: unless-stopped

  redis:
    image: redis:7-alpine
    command: redis-server --requirepass \${REDIS_PASSWORD}
    volumes:
      - redisdata:/data
    networks:
      - erp-net
    restart: unless-stopped

  backend:
    build: 
      context: ./src/Backend
      dockerfile: Dockerfile
    environment:
      - ConnectionStrings__DefaultConnection=Host=pgbouncer;Port=5432;Database=\${DB_NAME};Username=\${DB_USER};Password=\${DB_PASSWORD};Pooling=true;
      - Redis__ConnectionString=redis:6379,password=\${REDIS_PASSWORD}
      - ASPNETCORE_ENVIRONMENT=Production
    depends_on:
      - pgbouncer
      - redis
    networks:
      - erp-net
    restart: unless-stopped

  nginx:
    image: nginx:alpine
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./infrastructure/nginx.conf:/etc/nginx/nginx.conf:ro
      - ./src/Frontend/dist:/usr/share/nginx/html:ro # Vite Build Output
      - ./certs:/etc/nginx/certs:ro
    depends_on:
      - backend
    networks:
      - erp-net
    restart: unless-stopped

networks:
  erp-net:
    driver: bridge

volumes:
  pgdata:
  redisdata:
"@ | Out-File -FilePath "$infraDir\docker-compose.production.yml" -Encoding utf8

# 4. Nginx Config
@"
worker_processes auto;

events {
    worker_connections 1024;
}

http {
    include mime.types;
    default_type application/octet-stream;
    
    sendfile on;
    tcp_nopush on;
    tcp_nodelay on;
    keepalive_timeout 65;

    # Gzip Settings
    gzip on;
    gzip_types text/plain text/css application/json application/javascript text/xml application/xml application/xml+rss text/javascript;

    server {
        listen 80;
        server_name localhost;

        # Serve React/Vite Frontend
        location / {
            root /usr/share/nginx/html;
            index index.html index.htm;
            try_files \$uri \$uri/ /index.html; # SPA Routing
        }

        # Reverse Proxy to ASP.NET Core Backend
        location /api/ {
            proxy_pass http://backend:8080/;
            proxy_http_version 1.1;
            proxy_set_header Upgrade \$http_upgrade;
            proxy_set_header Connection keep-alive;
            proxy_set_header Host \$host;
            proxy_cache_bypass \$http_upgrade;
            proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto \$scheme;
        }
    }
}
"@ | Out-File -FilePath "$infraDir\nginx.conf" -Encoding utf8

# 5. Env Example
@"
# Database
DB_USER=postgres
DB_PASSWORD=SuperSecretDatabasePassword123!
DB_NAME=PosErpDb

# Redis
REDIS_PASSWORD=SuperSecretRedisPassword123!

# JWT
JWT_SECRET=YourSuperLongAndSecureJwtSecretKeyHereThatIsAtLeast32Bytes
JWT_ISSUER=PosErpSystem
JWT_AUDIENCE=PosErpFrontend
"@ | Out-File -FilePath "$infraDir\.env.example" -Encoding utf8

# 6. README
@"
# AI-Powered POS and ERP System

Enterprise-grade, offline-first Supermarket Point of Sale and ERP system.

## Tech Stack
- **Backend**: ASP.NET Core 8, EF Core, MediatR, Redis, Hangfire, PostgreSQL
- **Frontend**: React 18, Vite, Zustand, TailwindCSS, Dexie.js (Offline Storage)
- **Infrastructure**: Docker, Nginx, PgBouncer

## Modules Complete
1. **Core Billing (Offline-First)**: Dexie.js cart syncing to PostgreSQL backend.
2. **Inventory & Procurement**: Materialized views for stock, PO -> GRN 3-way matching.
3. **CRM, Loyalty & Offers**: JSON-based rules engine for best-discount calculation, digital wallet ledger.
4. **Accounting & GST**: Strict double-entry ledger posting on every transaction, E-Invoicing (IRN) hooks.
5. **Reporting & Dashboards**: Recharts integration with Hangfire-refreshed Materialized Views for instant KPI loads.

## Quick Start (Production Deployment)
1. Build the frontend: `cd src/Frontend && npm install && npm run build`
2. Copy `.env.example` to `.env` and set passwords.
3. Run docker compose: `docker-compose -f infrastructure/docker-compose.production.yml up -d`
4. Access the system at `http://localhost`.
"@ | Out-File -FilePath "$rootDir\README.md" -Encoding utf8

Write-Host "Go-Live Infrastructure Scaffolding Complete"
