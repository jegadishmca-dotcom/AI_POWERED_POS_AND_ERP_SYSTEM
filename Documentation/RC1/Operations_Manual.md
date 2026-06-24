# Apple Supermarket ERP - Operations Manual (RC1)

## 1. System Startup & Shutdown

### Graceful Startup
Navigate to the deployment directory and start detached:
```bash
sudo docker-compose up -d
```
Check the status of all containers:
```bash
sudo docker-compose ps
```

### Graceful Shutdown
To safely bring down the ERP system without corrupting ongoing POS transactions or database states:
```bash
sudo docker-compose down
```

---

## 2. Backup & Restore Procedures

### Database Backups (Target RPO: 15 minutes)
A cron job should be set up to execute the backup script automatically:
```bash
*/15 * * * * /path/to/Scripts/DR/db_backup.sh
```
This runs `pg_dump` and gzip, resulting in files like `poserp_20260624_1015.sql.gz`.

### Database Restore (Target RTO: 1 hour)
To restore a corrupted or failed system from a backup file:
```bash
cd /path/to/Scripts/DR
./db_restore.sh /var/backups/poserp/poserp_20260624_1015.sql.gz
```
*Note: Always verify the backup on the isolated `poserp_recovery` database before pushing to production.*

---

## 3. Incident Classification Matrix

| Classification | Definition | SLA Target | Escalation |
| :--- | :--- | :--- | :--- |
| **CRITICAL** | POS Billing has completely stopped. System offline or crashing. | 15 Minutes | IT Director, Senior Developer |
| **HIGH** | Inventory is incorrect or Sync failing. Billing active but degraded. | 1 Hour | System Administrator |
| **MEDIUM** | Reporting, Dashboards, or AI insights not generating correctly. | 4 Hours | DevOps / Data Team |
| **LOW** | Minor UI bugs or cosmetic defects not impacting business flow. | Next Sprint | Frontend Team |

---

## 4. Monitoring & Troubleshooting

### Daily Checks
- **Grafana Dashboard** (`http://<server-ip>:3000`): Check Request Rates, Error Rates, and Server CPU/RAM.
- **Hangfire Dashboard** (`http://<server-ip>:5000/hangfire`): Monitor background jobs (Failed vs Succeeded).

### Common Troubleshooting
- **Redis Disconnections**: If POS cart syncing fails, check Redis container logs `docker logs poserp-redis`. Restart if necessary.
- **Database Locks**: If POS experiences high latency, check PostgreSQL connection pool limits or slow queries via OpenTelemetry traces.
