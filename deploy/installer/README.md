# Apple Supermarket POS & ERP System — Production Installation Package

**Version:** 1.0.0  
**Release Tag:** `v1.0.0`  
**Vendor:** Unnamed Technologies Pvt. Ltd.  
**Classification:** Confidential — for licensed customers only  
**Support:** [support@unnamedtech.example.com](mailto:support@unnamedtech.example.com) | +91-44-4000-1234

---

## Package Overview

This directory contains the official production installation package for **Apple Supermarket POS & ERP System**, designed for turnkey deployment on an on-premise **Ubuntu 24.04 LTS** server.

### Included Files & Manifest

| File | Purpose |
| :--- | :--- |
| [`install.sh`](file:///opt/apple-pos/deploy/installer/install.sh) | Automated 15-step idempotent installer with pre-flight checks, Docker setup, TLS generation, and service registration. |
| [`uninstall.sh`](file:///opt/apple-pos/deploy/installer/uninstall.sh) | Production uninstaller supporting standard removal (data preserved) and complete data purge with safety backups. |
| [`vendor.conf`](file:///opt/apple-pos/deploy/installer/vendor.conf) | Vendor white-label identity, support channels, and release tag configuration. |
| [`brand.sh`](file:///opt/apple-pos/deploy/installer/brand.sh) | Re-branding utility that compiles documentation templates into customer deliverables. |
| [`docker-compose.prod.yml`](file:///opt/apple-pos/deploy/installer/docker-compose.prod.yml) | Security-hardened Docker Compose stack with isolated networks, resource limits, and service profiles. |
| [`.env.template`](file:///opt/apple-pos/deploy/installer/.env.template) | Production environment variable blueprint substituted by `install.sh`. |
| [`apple-pos-nginx.conf.template`](file:///opt/apple-pos/deploy/installer/apple-pos-nginx.conf.template) | Host-level Nginx reverse proxy template with TLS termination and security headers. |
| [`answers.conf.example`](file:///opt/apple-pos/deploy/installer/answers.conf.example) | Sample configuration file for non-interactive / unattended deployments. |
| [`INSTALLATION_GUIDE.md`](file:///opt/apple-pos/deploy/installer/INSTALLATION_GUIDE.md) | Comprehensive installation, networking, administration, and troubleshooting manual. |

---

## Quick Start (3 Steps)

### 1. Pre-Flight Verification
Verify your server runs **Ubuntu 24.04 LTS** with at least 3 GB of RAM (8 GB recommended):
```bash
lsb_release -a
free -h
```

### 2. Run the Installer
Execute the installation script with root privileges:
```bash
sudo bash install.sh
```

Follow the on-screen interactive prompts to configure:
- Store / Business Name
- Network Mode (`ip` for store LAN or `domain` for public FQDN with Let's Encrypt)
- Admin credentials and system timezone
- Optional SMTP notification relay

### 3. Log In to Web Interface
Once installation succeeds, open your browser and navigate to the server LAN IP:
```
https://<SERVER-IP>    (e.g. https://192.168.1.5)
```
*(Do not access via `http://localhost` due to single-origin production routing).*

- **Default Administrator**: `admin@supermarket.local` (Password displayed at end of install)
- **Default Cashier**: `cashier1@supermarket.local` / `Cashier@123`
- **Default Admin PIN**: `123456`

---

## Unattended / Automated Deployments

For automated rollouts via cloud-init, Ansible, or custom scripts:
```bash
cp answers.conf.example answers.conf
# Edit answers.conf with target settings
sudo bash install.sh --unattended --config answers.conf
```

---

## Common Administrative Operations

### Service Control
```bash
# Check stack status
sudo systemctl status apple-pos.service

# Restart application stack
sudo systemctl restart apple-pos.service

# View consolidated live container logs
sudo docker compose -f /opt/apple-pos/docker-compose.yml logs -f
```

### Manual Database Backup
```bash
sudo systemctl start apple-pos-backup.service
# Backups are saved to: /var/lib/apple-pos/backups/
```

### Uninstallation
```bash
# Standard uninstall (Preserves databases and backups)
sudo bash uninstall.sh

# Complete data purge (Irreversibly destroys all records with pre-purge safety backup)
sudo bash uninstall.sh --purge
```

---

## Technical Support

For deployment assistance, license inquiries, or technical support:
- **Email**: [support@unnamedtech.example.com](mailto:support@unnamedtech.example.com)
- **Phone**: +91-44-4000-1234
- **Hours**: Mon-Sat, 09:00-18:00 IST
- **Portal**: [https://www.unnamedtech.example.com](https://www.unnamedtech.example.com)
