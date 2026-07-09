# Phase 1 Engineering Sign-Off: Automated Off-Host Backup & Restore

This document serves as the permanent engineering sign-off for **Phase 1: Automated Off-Host Backup & Restore** of the Apple Supermarket POS & ERP System.

---

## Document Metadata
* **Project Name:** Apple Supermarket POS & ERP System
* **Phase Name:** Phase 1: Automated Off-Host Backup & Restore
* **Date & Time:** 09 July 2026, 16:15 IST (10:45 UTC)
* **Current Branch:** `release/v1.0-rc1`
* **Recommended Git Tag:** `v1.0.0-phase1-backup-complete`
* **Target Release:** `v1.0.0`
* **Status:** Frozen (Phase 1 technical verification successfully complete)

---

## 1. Scope Completed

* **Orchestration Scripting:** Deployed a robust, host-level backup runner `/home/jegadish/backups/backup_pos_db.sh` supporting database integrity verification via `pg_restore --list`.
* **Off-Host Ingestion Listener:** Deployed a workstation-side HTTP receiver service `backup_receiver.py` configured with strict path traversal validation and Grandfather-Father-Son (GFS) rotation rules.
* **Workstation Auto-Launch:** Implemented a Windows Startup launcher script `launch_backup_receiver.bat` to manage headless background execution (`pythonw`) on workstation boot.
* **Offline Python Alerting:** Deployed a database-decoupled Python `alert_sender.py` script on the host to manage alert notifications directly via SMTP/Resend API.

---

## 2. Security Improvements & Architecture

```mermaid
graph TD
    subgraph Ubuntu Production Server (192.168.1.5)
        cron[Cron Scheduler] -->|Triggers| script[backup_pos_db.sh]
        db[(posdb_live / posdb_audit)] -->|Dump raw binary| pgdump[pg_dump as pos_readonly]
        script -->|1. Run| pgdump
        pgdump -->|2. Verify Integrity| verify[pg_restore --list]
        verify -->|3. Encrypt| gpg[GPG Asymmetric Encryption]
        pubkey[backup_pubkey.asc] -->|Encrypts dump| gpg
        gpg -->|4. Copy encrypted dump| transfer[curl transfer]
    end
    subgraph Workstation (192.168.1.4)
        transfer -->|HTTP Upload over trusted LAN| receiver[backup_receiver.py]
        env[.env token] -->|Validates X-Backup-Token| receiver
        receiver -->|5. Save & Rotate| gfs[GFS Archive Rotation]
        privkey[backup_privkey.asc] -->|Decrypts dump off-host| decrypt[Workstation Decryption]
    end
```

### Key Security Postures:
1. **Asymmetric Key-Pair Encryption:** The server only holds the GPG public key. The private key resides **only** on the workstation and in your password manager, protecting backup contents in the event of server compromise.
2. **Read-Only Database Account:** Backup execution runs strictly under a dedicated `pos_readonly` PostgreSQL user.
3. **Decoupled Alerting:** Failures bypass the live backend container and PostgreSQL databases entirely, running as an offline Python task to prevent circular alert failures.
4. **LAN Binding:** The workstation receiver binds strictly to the local network IP `192.168.1.4` and filters input filenames against the strict regex `^posdb_(live|audit|uat)_\d{8}_\d{6}\.dump\.gpg$`.
5. **Hardened Directory Permissions:** Host backup directories and executables are set to `700` (`drwx------` / `-rwx------`), restricting read/write/execution strictly to the `jegadish` user.

---

## 3. E2E Backup & Restore Verification Summary

Verification of the database backup, off-host transfer, GFS rotation, and restoration pipeline has completed successfully:

### 3.1. Successful E2E Backup & Encrypted Off-Host Transfer
* **Asymmetric Encryption:** Checked that backups are encrypted on the host using the GPG public key, producing a secure `.gpg` format.
* **Successful Transfer:** The encrypted archive was copied to the workstation target `C:\Users\User\posdb-backups` over the local network via `curl`, verifying network routing and security token authentication.

### 3.2. Successful Decryption & Staging Restore
* **Workstation-Only Decryption:** The UAT backup (`posdb_uat_20260709_141833.dump.gpg`) was decrypted **locally on the workstation** using your local private key:
  ```bash
  gpg --decrypt --batch --output C:\Users\User\posdb-backups\posdb_uat_restore.dump C:\Users\User\posdb-backups\posdb_uat_20260709_141833.dump.gpg
  ```
* **Restore Execution:** The decrypted custom `.dump` was uploaded to the server, and `pg_restore` was successfully run against `posdb_uat` on the database container.

### 3.3. Multi-Table Row-Count Validation
We compared row counts of key tables immediately before the UAT backup was taken and immediately after the restore completed, validating **perfect data consistency**:

| Table Name | Before Restore Row Count | After Restore Row Count | Verification Status |
| :--- | :---: | :---: | :---: |
| `users` | 10 | 10 | **Match** |
| `products` | 35288 | 35288 | **Match** |
| `product_batches` | 1 | 1 | **Match** |
| `invoices` | 11 | 11 | **Match** |
| `sales_returns` | 6 | 6 | **Match** |
| `journal_entries` | 22 | 22 | **Match** |
| `customer_ledger` | 11 | 11 | **Match** |
| `supplier_ledger` | 0 | 0 | **Match** |
| `tax_transactions` | 22 | 22 | **Match** |

### 3.4. Production Mode Verification Check
* **Verification Timestamp:** 09 July 2026, 16:00 IST (10:30 UTC)
* **API Endpoint Checked:** `GET http://192.168.1.5:8000/api/environment/mode`
* **Response Body:** `{"activeMode":"LIVE","deploymentMode":"SelfHosted","isUat":false,"tenantName":null}` (Status 200 OK)

---

## 4. Architecture Decisions

### ADR 1: Asymmetric GPG Encryption
* **Decision:** Encrypt database backups on the server using only GPG public keys.
* **Rationale:** Removes the GPG private key (decryption key) from the server entirely. In the event of a server compromise, the attacker cannot decrypt historical backups. Decryption capability is restricted to secure off-server client workstations holding the corresponding GPG private key.

### ADR 2: Dedicated Read-Only PostgreSQL Backup Account
* **Decision:** Configure the host-level backup scripts to run `pg_dump` using a dedicated user (`pos_readonly`) instead of the database superuser (`posadmin`).
* **Rationale:** Follows the principle of least privilege. If the backup scripts or server filesystem are compromised, the read-only credentials do not grant modify, drop, or administrative access to the active database.

### ADR 3: Decoupled Alert Pipeline
* **Decision:** Replace the backend API dispatch route with a local host-level Python alerting script (`alert_sender.py`) querying a pre-cached JSON configuration file.
* **Rationale:** Eliminates the dependency on the live C# backend container and PostgreSQL database at alert time. If the backend container or database crashes (the exact failure condition backups must report), the alert system remains functional and dispatches notifications offline.

### ADR 4: Production-Only Scheduled Backups
* **Decision:** Configure cron tasks on the server to execute backups strictly for the production targets (`posdb_live` and `posdb_audit`).
* **Rationale:** Keeps backups focused on active client records and prevents automated test environments (UAT) from filling up host disk partitions or local workstation archives.

### ADR 5: Trusted-LAN Authenticated Backup Receiver
* **Decision:** Deploy a lightweight Python HTTP receiver on the workstation bound to the LAN interface IP (`192.168.1.4`) on port `9000`.
* **Rationale:** Limits exposure by binding to a specific internal network interface rather than `0.0.0.0`. Validates incoming uploads using a high-entropy secret token passed in HTTP headers, preventing unauthorized upload attempts.

---

## 5. Production Readiness & Warnings

* **Production Environment Status:** Verified that the active production environment is successfully running in **LIVE** mode.
* **Cron Schedules Verified:** Confirmed that only `posdb_live` (every 6 hours) and `posdb_audit` (daily at 2:30 AM) are registered in the host's cron. No automated UAT backups are scheduled.

### Outstanding Manual Actions (User Responsibility):
* **Resend Account Domain/Recipient Verification:** You must log in to the Resend console and verify either `jegadishmca@gmail.com` or your email domain.
* **Gmail App Password Manual Rotation:** You must manually regenerate a new App Password for `EMAIL_SENDER_PASSWORD` in Google Account settings and update your `.env` files.

### Remaining Technical Debt:
* **Terminal billing concurrent checks:** Add concurrency billing locks to prevent sequence collisions.
* **Hardcoded PepperKey:** The hardcoded `PepperKey` in `EmailSettingsManager.cs` should be migrated to server-level configuration variables.

---

## 6. Rollback Procedure

In the event of an operational failure after tagging or deploying Phase 1 changes:
1. **Restore Git State:** Check out the previous stable commit tag in git.
2. **Off-Host Decryption:** Decrypt the latest verified backup `.gpg` file on your workstation using the GPG private key.
3. **Database Restoration:** Upload the decrypted dump file to the host and restore the database using `pg_restore` with standard drop/recreate commands.
4. **Service Restart:** Rebuild and start the containers using:
   ```bash
   docker compose down
   docker compose up -d --build
   ```
5. **Verification:** Validate login checks and verify that the endpoint `/api/environment/mode` returns `activeMode: LIVE`.

---

## 7. Recommended Next Phase: Phase 2
* **Recommended Next Phase:** **Phase 2: Database Concurrency & Race-Condition Hardening** (billing invoice sequences validation, transactional connection pooling improvements, and high-concurrency checkout E2E stress testing).
