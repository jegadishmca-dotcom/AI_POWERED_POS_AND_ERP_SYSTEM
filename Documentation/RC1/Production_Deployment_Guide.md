# Apple Supermarket ERP - Version 1.0 (RC1) Production Deployment Guide

## 1. Hardware Prerequisites

### Local Server Setup
- **Processor**: Intel i5 (Minimum) / Intel i7 (Preferred)
- **Memory**: 32 GB RAM (Minimum) / 64 GB RAM (Preferred)
- **Storage**: SSD Storage (RAID Preferred for redundancy)
- **OS**: Ubuntu Server LTS (22.04 or 24.04)
- **Power**: UPS with minimum 30 minutes backup (Mandatory)

### POS Terminal Specs
- **Processor**: Intel i5
- **Memory**: 8 GB RAM (16 GB Preferred)
- **Storage**: SSD
- **OS**: Windows 11 Pro
- **Peripherals Supported**: 
  - USB HID Barcode Scanners (1D EAN-13, 2D QR Code) - e.g. Honeywell, Zebra.
  - ESC/POS, USB, or Network Receipt Printers - e.g. Epson, TVS.
  - ESC/POS Trigger Cash Drawers.

---

## 2. Environment Configuration

Copy the sample `.env` file and populate it carefully. **Do not store secrets in source control.**

```env
# Database Configuration
DB_HOST=postgres
DB_PORT=5432
DB_NAME=poserp
DB_USER=postgres
DB_PASSWORD=<SECURE_STRONG_PASSWORD>

# Redis Configuration
REDIS_HOST=redis
REDIS_PORT=6379
REDIS_PASSWORD=<SECURE_STRONG_PASSWORD>

# JWT Authentication
JWT__Issuer=AppleSupermarket_AuthServer
JWT__Audience=AppleSupermarket_SPA
JWT__Secret=<SECURE_32_CHAR_SECRET_KEY>

# Observability / Monitoring
PROMETHEUS_PORT=9090
GRAFANA_PORT=3000
GRAFANA_PASSWORD=<SECURE_PASSWORD>
```

---

## 3. Docker Compose Deployment

The application is deployed via a unified `docker-compose.yml` defining the following services:
- **api**: .NET 8 Backend Application
- **spa**: React Frontend (Nginx static serving)
- **postgres**: PostgreSQL Database
- **redis**: Redis Cache
- **prometheus**: Local Metrics Scraping
- **grafana**: Metric Visualizations

### Execution Steps
1. Install Docker & Docker Compose on Ubuntu.
2. Clone repository & configure `.env`.
3. Run deployment:
   ```bash
   sudo docker-compose up -d --build
   ```
4. Verify services:
   ```bash
   sudo docker-compose ps
   ```

---

## 4. HTTPS & Reverse Proxy Configuration
It is strictly recommended to sit the application behind an Nginx reverse proxy with SSL termination.
1. Install `nginx` and `certbot`.
2. Configure `/etc/nginx/sites-available/poserp.conf`.
3. Run Let's Encrypt: `sudo certbot --nginx -d pos.applesupermarket.local`.
