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
1. Build the frontend: cd src/Frontend && npm install && npm run build
2. Copy .env.example to .env and set passwords.
3. Run docker compose: docker-compose -f infrastructure/docker-compose.production.yml up -d
4. Access the system at http://localhost.
