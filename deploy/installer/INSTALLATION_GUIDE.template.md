# {{PRODUCT_NAME}} — Production Installation & Administration Guide

**Document Version:** 1.0.0  
**Product Version:** {{PRODUCT_VERSION}}  
**Release Tag:** `{{RELEASE_TAG}}`  
**Vendor:** {{VENDOR_COMPANY}}  
**Classification:** {{VENDOR_CLASSIFICATION}}  
**Target Platform:** Ubuntu 24.04 LTS (x86_64)  
**Copyright:** © {{VENDOR_COPYRIGHT_YEAR}} {{VENDOR_COMPANY}}. All rights reserved.

---

## Technical Support & Contacts

| Channel | Contact Information |
| :--- | :--- |
| **Vendor Support Desk** | [{{VENDOR_SUPPORT_EMAIL}}](mailto:{{VENDOR_SUPPORT_EMAIL}}) |
| **Direct Phone Line** | {{VENDOR_SUPPORT_PHONE}} |
| **Customer Portal** | [{{VENDOR_WEBSITE}}]({{VENDOR_WEBSITE}}) |
| **Operating Hours** | {{VENDOR_SUPPORT_HOURS}} |
| **Support SLA** | {{VENDOR_SLA}} |
| **Corporate Address** | {{VENDOR_ADDRESS}} |

---

## Table of Contents
1. [Architecture Overview](#1-architecture-overview)
2. [Pre-Requisites & System Requirements](#2-pre-requisites--system-requirements)
3. [Network & Firewall Requirements](#3-network--firewall-requirements)
4. [Installation Procedures](#4-installation-procedures)
5. [Post-Installation Verification](#5-post-installation-verification)
6. [Configuring Additional POS Terminals & Cashier Workstations](#6-configuring-additional-pos-terminals--cashier-workstations)
7. [Monitoring & Telemetry](#7-monitoring--telemetry)
8. [Backup & Disaster Recovery](#8-backup--disaster-recovery)
9. [Uninstallation & Purge](#9-uninstallation--purge)
10. [Troubleshooting & Diagnostics](#10-troubleshooting--diagnostics)

---

## 1. Architecture Overview

{{PRODUCT_NAME}} is deployed as a hardened, containerized multi-tier on-premise application orchestrated via Docker Compose, fronted by a host-level Nginx reverse proxy providing TLS termination.

```
                              PUBLIC INTERNET / LAN
                                       │
                                       │ Port 80 (HTTP ──► 301 HTTPS Redirect)
                                       │ Port 443 (HTTPS TLS Termination)
                                       ▼
┌──────────────────────────────────────────────────────────────────────────────────┐
│ UBUNTU HOST OS (Bare-Metal or Virtual Machine)                                   │
│                                                                                  │
│  Host-Level Nginx Reverse Proxy (/etc/nginx/sites-available/{{PRODUCT_SLUG}}.conf) │
│  ├── TLS Hardening (TLSv1.2 / TLSv1.3, Strict Ciphers, HSTS, Security Headers)   │
│  ├── Public Entry: 0.0.0.0:80, 0.0.0.0:443                                      │
│  └── Internal Forwarding ──► 127.0.0.1:8000 (Internal Stack Gateway)             │
│                                                                                  │
│  Systemd Service Units:                                                          │
│  ├── {{PRODUCT_SLUG}}.service        (Manages Docker Compose lifecycle)         │
│  └── {{PRODUCT_SLUG}}-backup.timer   (Nightly automated database dump at 02:00) │
└──────────────────────────────────────┬───────────────────────────────────────────┘
                                       │ 127.0.0.1:8000
                                       ▼
┌──────────────────────────────────────────────────────────────────────────────────┐
│ DOCKER CONTAINER STACK (Docker Compose)                                          │
│                                                                                  │
│  ┌────────────────────────────────────────────────────────────────────────────┐  │
│  │ Internal Nginx Gateway (apple-pos-nginx: 127.0.0.1:8000 ──► internal 80)   │  │
│  │  ├── /           ──► pos_frontend:80 (React Single Page Application)       │  │
│  │  ├── /api/       ──► pos_backend:8080/api/ (.NET 8 Core Web API)           │  │
│  │  └── /api/health ──► pos_backend:8080/health (Backend Health Check)        │  │
│  └─────────────────────────────────────┬──────────────────────────────────────┘  │
│                                        │                                         │
│               Internal Docker Network  │ (pos_internal - internal: true)         │
│         ┌──────────────────────────────┼──────────────────────────────┐          │
│         ▼                              ▼                              ▼          │
│  ┌───────────────┐              ┌───────────────┐              ┌──────────────┐  │
│  │ pos_backend   │◄────────────►│ pgbouncer     │◄────────────►│ postgres     │  │
│  │ .NET 8 Web API│              │ Pooler (6432) │              │ PG 16.4      │  │
│  └───────┬───────┘              └───────────────┘              └──────────────┘  │
│          │                             │                              │          │
│          ▼                             ▼                              ▼          │
│  ┌───────────────┐              ┌───────────────┐              ┌──────────────┐  │
│  │ redis         │              │ prometheus    │              │ grafana      │  │
│  │ Cache 7.2     │              │ (Optional)    │              │ (Optional)   │  │
│  └───────────────┘              └───────────────┘              └──────────────┘  │
│          │                                                                       │
│          │ Egress Network (pos_egress - bridge)                                  │
│          ▼                                                                       │
│  ┌───────────────┐                                                               │
│  │ ollama        │ (Optional AI Profile — Isolated container)                    │
│  └───────────────┘                                                               │
└──────────────────────────────────────────────────────────────────────────────────┘
```

### Key Security Isolation Guarantees
1. **Zero Host Exposure of Internal Services**: PostgreSQL (`5432`), PgBouncer (`6432`), Redis (`6379`), Backend (`8080`), and Prometheus (`9090`) do **NOT** bind to any host network interface. They exist solely on an isolated Docker network (`pos_internal`) configured with `internal: true`.
2. **Single Controlled Host Port**: Only the Host Nginx reverse proxy binds to public ports `80` and `443`. The internal Docker stack is reachable only via loopback at `127.0.0.1:8000`.
3. **Strict Egress Control**: Only containers requiring outbound communication (`pos_backend` for outbound email delivery and `ollama` for downloading open-source model weights) are attached to `pos_egress`.

---

## 2. Pre-Requisites & System Requirements

### Hardware Sizing Guidelines

| Component | Standard Retail Deployment (Base POS & ERP) | AI Co-Pilot Enabled Deployment (`--enable-ai`) |
| :--- | :--- | :--- |
| **CPU Architecture** | x86_64 (64-bit Intel or AMD) | x86_64 (AVX2 instruction support required) |
| **Processor Cores** | Minimum 2 Cores (4 Cores Recommended) | Minimum 4 Cores (8 Cores Recommended) |
| **System Memory (RAM)** | Minimum 3 GB (8 GB Recommended) | Minimum 8 GB (16 GB Recommended) |
| **Disk Storage** | Minimum 20 GB SSD (50 GB NVMe Recommended) | Minimum 50 GB SSD (100 GB NVMe Recommended) |
| **Network Interface** | 1 Gbps Ethernet LAN Card | 1 Gbps Ethernet LAN Card |

### Operating System & User Privileges
- **Target OS**: **Ubuntu 24.04 LTS (Noble Numbat)** Server Edition (Fresh installation recommended). Ubuntu 22.04 LTS is supported.
- **Account Permissions**: The installer requires root privileges via `sudo`. The installer creates an isolated service user and applies strict POSIX permissions (`0750` on data, `0600` on credentials).
- **Docker Requirement**: The installation script automatically installs Docker Engine and Docker Compose plugin from Docker's official apt repository. Pre-existing Docker installs via Snap or `docker.io` will be flagged by pre-flight checks.

---

## 3. Network & Firewall Requirements

### Inbound Port Access (Host Server)

| Port | Protocol | Purpose | Source Allowed |
| :--- | :--- | :--- | :--- |
| **80** | TCP | HTTP traffic (automatically redirected to HTTPS 443; ACME challenge webroot) | Store LAN & Cashier Terminals (and Public Internet if using Let's Encrypt) |
| **443** | TCP | HTTPS Web Interface, REST API, SignalR Hubs | Store LAN, Manager Workstations, Cashier Terminals |
| **22** | TCP | Secure Shell (SSH) system administration & remote management | Internal IT Subnet / Bastion Host only |

> [!IMPORTANT]
> **Inbound Internet Access Requirement for Let's Encrypt (HTTP-01 Challenge)**  
> If you configure **Domain Mode with Let's Encrypt**, the Let's Encrypt validation authorities must be able to reach your on-premise server on **inbound TCP port 80** from the public internet to verify domain ownership.  
> - **Edge Router / Firewall**: You must configure a Port Forwarding / NAT rule on your customer site's edge gateway:  
>   `Public WAN IP : Port 80 (TCP)  ──►  Server Local IP : Port 80 (TCP)`  
>   `Public WAN IP : Port 443 (TCP) ──►  Server Local IP : Port 443 (TCP)`  
> - **DNS**: The Fully Qualified Domain Name (e.g. `pos.supermarket.com`) must resolve publicly to your site's external static WAN IP.  
> - **Air-Gapped / Isolated LANs**: If your server does not have a public static IP or cannot accept inbound traffic on port 80, **you must choose IP Mode (Self-Signed TLS)** during installation.

### Outbound Firewall Egress Matrix

This table lists all possible outbound destinations initiated by the server. Customers operating strict outbound firewalls should apply these rules into their network change control tickets:

| Destination Host / IP | Protocol & Port | Purpose | Default Status | How Enabled / Selected |
| :--- | :--- | :--- | :--- | :--- |
| **Customer SMTP Relay** *(e.g. `smtp.office365.com`, `smtp.gmail.com`, or internal IP)* | TCP `587` (STARTTLS) or `465` (SSL) or `25` (Plaintext) | Nightly EOD Sales Summary & Admin alert notifications | **Disabled** (Empty by default) | Configured in `install.sh` prompt or via Web UI (*Settings → Email Settings → Delivery Method: SMTP*). |
| **`api.postmarkapp.com`** | TCP `443` (HTTPS) | Optional cloud transactional email delivery | **Disabled** (Requires API Token) | Selected in Web UI (*Delivery Method: Postmark*). |
| **`api.mailgun.net`** | TCP `443` (HTTPS) | Optional cloud transactional email delivery | **Disabled** (Requires API Key & Domain) | Selected in Web UI (*Delivery Method: Mailgun*). |
| **`api.resend.com`** | TCP `443` (HTTPS) | Optional cloud transactional email delivery | **Disabled** (Requires API Key) | Selected in Web UI (*Delivery Method: Resend*). |
| **`registry.ollama.ai`** *(and `*.ollama.ai`)* | TCP `443` (HTTPS) | Downloading AI Co-Pilot model weights (`llama3.2`, `qwen2.5`) | **Disabled** | Enabled only if `--enable-ai` flag is passed to `install.sh`. |
| **`acme-v02.api.letsencrypt.org`** | TCP `443` (Outbound HTTPS) / TCP `80` (Inbound HTTP) | Automatic TLS certificate issuance and 60-day renewals | **Disabled** (Self-signed IP mode default) | Enabled only if operator selects Let's Encrypt domain access during installation. |
| **`github.com`**, **`*.ubuntu.com`**, **`download.docker.com`** | TCP `443`, TCP `80` | Initial installer dependencies (Docker Engine, git release clone) | **Active during installation only** | Required only during execution of `install.sh`. Can be completely blocked post-install. |

*Note: There are NO external GST API calls. Indian GST tax slabs and HSN classification tables are seeded locally inside the database.*

---

## 4. Installation Procedures

### Step 1: Pre-Installation Checklist
1. Log in to the target Ubuntu 24.04 LTS server via SSH as a user with `sudo` privileges.
2. Verify system time and timezone:
   ```bash
   timedatectl status
   ```
3. Extract or navigate to the installation package directory containing `install.sh`, `vendor.conf`, and `docker-compose.prod.yml`.

### Step 2: Interactive Installation
To run the automated interactive installer, execute:
```bash
sudo bash install.sh
```

#### Installer Command-Line Flags
The installer supports the following optional flags:

| Flag | Description |
| :--- | :--- |
| `--dry-run` | Simulates all pre-flight checks and configuration without making changes. |
| `--verbose` | Enables detailed debug and diagnostic output. |
| `--tag <tag>` | Specifies an exact git release tag to deploy (overrides `vendor.conf`). |
| `--no-monitoring` | Skips installation of Prometheus and Grafana containers to conserve RAM. |
| `--enable-ai` | Deploys the local Ollama AI Co-Pilot container (`llama3.2:1b`). |
| `--skip-preflight` | Bypasses hardware CPU/RAM checks (useful for staging/testing VMs). |
| `--unattended` | Executes non-interactively using an answers configuration file. |
| `--config <file>` | Path to configuration file (required when using `--unattended`). |
| `--uninstall` | Invokes the uninstaller script (`uninstall.sh`). |

### Step 3: Interactive Configuration Prompts
During execution, `install.sh` presents the following interactive prompts:

1. **Company / Store Display Name**: Enter the business trade name (e.g. `GreenValley Supermarket`).
2. **Access Mode (`domain` or `ip`)**:
   - `ip` *(Recommended for on-premise store networks)*: Uses the server's static LAN IP address (e.g. `192.168.1.5`). Automatically generates a 2048-bit RSA self-signed TLS certificate valid for 365 days.
   - `domain`: Requires an internet-resolvable FQDN (e.g. `pos.greenvalley.com`). Automatically executes Certbot against Let's Encrypt and configures a systemd renewal hook.
3. **Administrator Email**: Used for administrative alerts, system reports, and TLS renewal notifications.
4. **Administrator Password**: Enter a master admin password (minimum 8 characters, requiring letters and digits) or leave blank to auto-generate a secure 20-character secret.
5. **System Timezone**: Default: `Asia/Kolkata`.
6. **SMTP / Email Notifications (Optional)**: If outbound email is required for EOD reports, enter SMTP Host, Port (587), Username, and Password. Press **Enter** to skip if email is not needed.

### Step 4: Unattended Automated Installation
For automated provisioning via Ansible, Terraform, or cloud-init:
1. Copy the sample answers file:
   ```bash
   cp answers.conf.example answers.conf
   ```
2. Edit `answers.conf` with your site configuration and secrets.
3. Run the installer:
   ```bash
   sudo bash install.sh --unattended --config answers.conf
   ```

---

## 5. Post-Installation Verification

### Step 1: Check Systemd Unit Status
The application stack runs under a systemd supervisor unit that manages container restarts upon host reboot:
```bash
sudo systemctl status {{PRODUCT_SLUG}}.service
```
Verify the nightly backup timer is active:
```bash
sudo systemctl status {{PRODUCT_SLUG}}-backup.timer
```

### Step 2: Validate HTTP & API Health Probes
Run direct curl checks against the host Nginx proxy:
```bash
# 1. Verify Host Nginx reverse proxy response
curl -k -i https://127.0.0.1/nginx-health

# 2. Verify Backend Core API health endpoint
curl -k -s https://127.0.0.1/api/health
# Expected JSON output: {"status":"Healthy","database":"Healthy","redis":"Healthy"}
```

### Step 3: Accessing the Web Application

> [!CAUTION]
> **Mandatory Access Policy: Always Access via Server IP / Domain**  
> Administrators and cashiers must **ALWAYS** navigate to the server's configured LAN IP or domain (e.g. `https://192.168.1.5` or `https://pos.yourstore.com`).  
> **DO NOT** access the application via `http://localhost` or `http://127.0.0.1` on the server desktop. By design, single-origin security routing rejects localhost origins with `SERVER_URL_MISSING`.

1. Open a modern web browser (Google Chrome or Microsoft Edge recommended) on any client machine on the store network.
2. Navigate to: `https://<SERVER-IP>` (e.g. `https://192.168.1.5`).
3. If using self-signed TLS (`ip` mode), your browser will display an initial security warning (`NET::ERR_CERT_AUTHORITY_INVALID`). Click **Advanced** and select **Proceed to <SERVER-IP> (unsafe)**.
4. Log in using the generated administrative credentials:
   - **Default Admin Account**: `admin@supermarket.local`
   - **Password**: Displayed in the installation summary and stored in `/opt/{{PRODUCT_SLUG}}/.env`.
   - **Default Cashier Account**: `cashier1@supermarket.local` (Password: `Cashier@123`).
   - **Default Master Admin PIN**: `123456`.

---

## 6. Configuring Additional POS Terminals & Cashier Workstations

{{PRODUCT_NAME}} supports multi-terminal retail setups. Satellite cashier billing terminals, barcode scanning counters, and manager tablets connect directly across the store LAN without installing software on client machines.

### Step-by-Step Terminal Setup

```
┌──────────────────────────┐        Store LAN (Wi-Fi / Ethernet)        ┌──────────────────────────┐
│ Central Server           │◄───────────────────────────────────────────│ Cashier Terminal 1       │
│ https://192.168.1.5      │                                            │ Google Chrome / Edge     │
└────────────┬─────────────┘                                            └──────────────────────────┘
             │
             │                                                          ┌──────────────────────────┐
             └──────────────────────────────────────────────────────────│ Handheld Tablet / Scanner│
                                                                        │ Safari / Chrome Tablet   │
                                                                        └──────────────────────────┘
```

1. **Connect Client to Store Network**: Ensure the client terminal is connected to the same local area network or retail VLAN as the server.
2. **Open Browser**: Launch Google Chrome or Microsoft Edge on the terminal.
3. **Navigate to Server IP**: Enter `https://<SERVER-IP>` (e.g. `https://192.168.1.5`).
4. **Accept Self-Signed Certificate**:
   - On Chrome/Edge: Click **Advanced** ──► **Proceed to 192.168.1.5**.
   - *(Enterprise Best Practice)*: Export the root certificate from `/etc/ssl/{{PRODUCT_SLUG}}/fullchain.pem` on the server and deploy it to Windows/macOS **Trusted Root Certification Authorities** store across all cashier terminals.
5. **Lock in Server IP via Settings (Built-in Client Configuration)**:
   - If running a dedicated standalone client build, log in as Manager or Admin.
   - Navigate to **Settings** ──► **Connection** tab.
   - In the **Server IP Address / Domain** field, enter `https://192.168.1.5`.
   - Click **Test Ping** (verifies communication with emerald checkmark).
   - Click **Save & Connect**. The browser saves the endpoint to local storage (`pos_server_ip`) and reloads automatically.
6. **Enable Cashier Mode**: Cashiers log in with their assigned username (`cashier1@supermarket.local`) and are routed directly to the high-speed touch billing screen (`/pos`).

---

## 7. Monitoring & Telemetry

If monitoring was enabled during installation (`ENABLE_MONITORING=true`), Prometheus and Grafana containers collect operational metrics.

### Secure SSH Tunnel Access
To maintain zero external attack surface, Grafana (`3000`) and Prometheus (`9090`) do not expose public ports. Access them from your management laptop via an encrypted SSH tunnel:

```bash
# Forward port 3000 to your local machine
ssh -L 3000:localhost:3000 user@<SERVER-IP>
```
1. Open your local browser and navigate to `http://localhost:3000`.
2. **Username**: `admin`
3. **Password**: Displayed in installation summary or retrieved from `/opt/{{PRODUCT_SLUG}}/.env` (`GRAFANA_PASSWORD`).
4. Pre-configured dashboards provide visibility into:
   - PostgreSQL connection pool saturation and transaction commit rates.
   - Redis cache hit/miss ratio.
   - POS checkout latency and HTTP request throughput.
   - Hardware CPU, memory, and disk I/O metrics.

---

## 8. Backup & Disaster Recovery

### Automated Nightly Backups
A systemd timer triggers a full database dump every night at 02:00:
- **Timer Unit**: `{{PRODUCT_SLUG}}-backup.timer`
- **Backup Script**: `/opt/{{PRODUCT_SLUG}}/deploy/installer/backup.sh`
- **Storage Location**: `/var/lib/{{PRODUCT_SLUG}}/backups/`
- **Retention Policy**: Backups older than 30 days are automatically purged.
- **Compression**: `gzip` compressed SQL dump (`posdb_live-YYYYMMDD_HHMMSS.sql.gz`).

### Manual On-Demand Backup
To trigger an immediate backup before upgrades or maintenance:
```bash
sudo systemctl start {{PRODUCT_SLUG}}-backup.service
```
Check the backup log:
```bash
sudo cat /var/log/{{PRODUCT_SLUG}}/backup.log
```

### Complete Disaster Recovery Restore Procedure
In the event of hardware failure, disk corruption, or server migration:

1. **Provision a new Ubuntu 24.04 LTS server** and run `install.sh` to initialize Docker, Nginx, and system dependencies.
2. **Stop the backend and application stack**:
   ```bash
   sudo systemctl stop {{PRODUCT_SLUG}}.service
   ```
3. **Transfer your latest backup archive** to the new server (e.g. `/tmp/posdb_live-restore.sql.gz`).
4. **Restore the PostgreSQL database**:
   ```bash
   # Uncompress and pipe directly into the running postgres container
   gunzip -c /tmp/posdb_live-restore.sql.gz | sudo docker exec -i {{PRODUCT_SLUG}}-postgres psql -U posadmin -d posdb_live
   ```
5. **Restore configuration**: Ensure `/var/lib/{{PRODUCT_SLUG}}/config/operation_mode.json` contains:
   ```json
   {
     "ActiveMode": "LIVE",
     "TokenVersion": 1
   }
   ```
6. **Restart the application stack**:
   ```bash
   sudo systemctl start {{PRODUCT_SLUG}}.service
   ```
7. **Verify data integrity**: Log in to the web interface and verify transaction history, stock inventory, and user accounts.

---

## 9. Uninstallation & Purge

The installation package includes a standalone uninstaller script: [`uninstall.sh`](file:///opt/{{PRODUCT_SLUG}}/deploy/installer/uninstall.sh).

### Scenario A: Standard Uninstall (Data Preserved)
Stops containers, removes systemd units, removes host Nginx proxy, and removes application binaries, but **PRESERVES** all database records, backups, and logs:
```bash
sudo bash uninstall.sh
```
*Prompt: Confirms with `(y/N)`. Preserves `/var/lib/{{PRODUCT_SLUG}}`.*

### Scenario B: Complete Data Purge (Destructive)
Irreversibly deletes all databases, Docker volumes, backups, and container images:
```bash
sudo bash uninstall.sh --purge
```

#### Purge Safety Safeguards
1. **Interactive Slug Confirmation**: The operator must type the exact product slug (`{{PRODUCT_SLUG}}`) to proceed.
2. **Automated Emergency Pre-Purge Backup**: Before deleting volumes, `uninstall.sh` automatically dumps the live database to:
   `/var/backups/{{PRODUCT_SLUG}}-pre-purge-YYYYMMDD_HHMMSS.sql.gz`  
   *This archive is stored outside the product directories and will survive the purge.*
3. **Unattended Protection**: When using `--unattended --purge`, the script will refuse to execute unless `--force-data-loss` is explicitly passed.

---

## 10. Troubleshooting & Diagnostics

### Diagnostic Commands Quick Reference

```bash
# Check status of all running containers
sudo docker compose -f /opt/{{PRODUCT_SLUG}}/docker-compose.yml ps

# View live consolidated logs
sudo docker compose -f /opt/{{PRODUCT_SLUG}}/docker-compose.yml logs -f --tail=100

# View backend API container logs
sudo docker logs -f {{PRODUCT_SLUG}}-backend

# Test Host Nginx configuration
sudo nginx -t

# View Host Nginx access and error logs
sudo tail -f /var/log/nginx/access.log /var/log/nginx/error.log
```

---

### Common Issues & Resolutions

#### Issue 1: Backend can't connect to database after container or volume reset
- **Symptom**: Backend container fails health checks (`curl: (7) Failed to connect to localhost:8080/health`), and logs display `Npgsql.PostgresException: 3D000: database "posdb_uat" does not exist`.
- **Root Cause**: The configuration file `/var/lib/{{PRODUCT_SLUG}}/config/operation_mode.json` is missing or unreadable (often after a volume prune or manual config directory deletion), causing the backend engine to default to UAT mode and seek `posdb_uat`.
- **Resolution**: Recreate the file with root ownership and restart the backend:
  ```bash
  sudo mkdir -p /var/lib/{{PRODUCT_SLUG}}/config
  sudo tee /var/lib/{{PRODUCT_SLUG}}/config/operation_mode.json << 'EOF'
  {
    "ActiveMode": "LIVE",
    "TokenVersion": 1
  }
  EOF
  sudo chmod 640 /var/lib/{{PRODUCT_SLUG}}/config/operation_mode.json
  sudo docker compose -f /opt/{{PRODUCT_SLUG}}/docker-compose.yml restart pos_backend
  ```

---

#### Issue 2: Nightly SMTP exception logged when email is unconfigured
- **Symptom**: Logs show `[DailyReportEmailService] [ERROR] Background worker loop encountered exception: SMTP credentials are not configured.` once per night at 23:59 IST.
- **Root Cause**: Outbound email was skipped during installation. This is expected and completely harmless — no core POS billing or inventory function depends on email.
- **Resolution**: If daily sales report emails are wanted, configure SMTP settings in the Web UI under **Settings ──► Email Settings**. If daily report emails are not needed, safely ignore this log entry; it does not impact POS operations.

---

#### Issue 3: Browser shows "Backend API URL is not configured. Please verify that VITE_API_URL is set..."
- **Symptom**: Opening the web interface displays an error box on the login screen regarding `SERVER_URL_MISSING`.
- **Root Cause**: Accessing the web application via `http://localhost` or `http://127.0.0.1` on the server machine itself. Single-origin production routing requires network origin resolution.
- **Resolution**: Always access the application via the server's configured static LAN IP (e.g. `https://192.168.1.5`) or FQDN domain from your browser.

---

#### Issue 4: Port 80 or 443 Conflict During Installation
- **Symptom**: `install.sh` pre-flight checks report `Port 80 is already in use` or `Port 443 is already in use`.
- **Root Cause**: Another web server (e.g., Apache2, Caddy, or a previous Nginx instance) is running on the host.
- **Resolution**: Identify and disable conflicting services:
  ```bash
  sudo lsof -i :80 -i :443
  # If Apache2 is running:
  sudo systemctl stop apache2 && sudo systemctl disable apache2
  ```

---

#### Issue 5: Nginx 502 Bad Gateway
- **Symptom**: Browser displays `502 Bad Gateway` when loading `https://<SERVER-IP>`.
- **Root Cause**: The Docker container stack is still initializing or the internal containerized Nginx gateway on port `8000` is down.
- **Resolution**: Check container health:
  ```bash
  sudo docker ps --format 'table {{.Names}}\t{{.Status}}'
  ```
  Allow up to 45 seconds for initial database schema migration runner execution in `pos_backend`.
