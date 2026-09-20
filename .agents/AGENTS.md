<RULE[user_global]>
The current Git working branch is 'release/v1.0-rc1'. ALWAYS use this branch name (instead of main or master) in all git pull and git push command examples.
</RULE[user_global]>

<RULE[project]>
## Ubuntu Production Server — Docker Deployment

The production server for this ERP system (Apple Supermarket POS) runs on an **Ubuntu server using Docker**.
When providing deployment, update, or restart instructions for the production server, ALWAYS use this exact sequence:

```bash
# 1. Pull the latest code
cd /opt/apple-pos   # or the actual project directory on the server
git pull origin release/v1.0-rc1

# 2. Rebuild and restart all containers
docker compose down
docker compose up -d --build
```

NEVER suggest `systemctl restart`, `dotnet run`, `npm run dev`, or any non-Docker commands for the production server.
ALWAYS use `docker compose` (not the deprecated `docker-compose` syntax) in all examples.
SQL migrations run automatically on backend container startup via the migration runner in Program.cs — never tell the user to run SQL manually on the server unless the migration runner is explicitly broken.

## Mandatory Pre-Deployment Backup & Scratch Restore-Verification Protocol

Before performing any production release, deployment, or database migration, an automated backup and test restore MUST be executed to guarantee disaster recoverability.

**CRITICAL RULE**: NEVER run `docker exec -t` (with TTY) when capturing binary `pg_dump` streams, as the pseudo-TTY driver converts `0x0A` (LF) to `0x0D 0x0A` (CRLF), silently corrupting binary dumps. ALWAYS dump directly to a file inside the container using `-f` or non-interactive redirection (`docker exec -i` without `-t`).

**Mandatory 3-Step Procedure**:
```bash
# 1. Dump database directly to container storage (never pipe through TTY)
docker exec pos_postgres pg_dump -U posadmin -Fc -f /tmp/pre_deploy_posdb_uat.dump posdb_uat
docker exec pos_postgres pg_dump -U posadmin -Fc -f /tmp/pre_deploy_posdb_live.dump posdb_live

# 2. MANDATORY: Verify restore into an isolated scratch database
docker exec -i pos_postgres createdb -U posadmin posdb_test_restore
docker exec -i pos_postgres pg_restore -U posadmin -d posdb_test_restore --no-owner --role=posadmin /tmp/pre_deploy_posdb_uat.dump
docker exec -i pos_postgres psql -U posadmin -d posdb_test_restore -c "SELECT count(*) FROM products; SELECT count(*) FROM journal_entries;"
docker exec -i pos_postgres dropdb -U posadmin posdb_test_restore

# 3. Copy verified dumps to host archive
docker cp pos_postgres:/tmp/pre_deploy_posdb_uat.dump /home/jegadish/backups/posdb_uat_backup_$(date +%Y%m%d_%H%M%S).dump
docker cp pos_postgres:/tmp/pre_deploy_posdb_live.dump /home/jegadish/backups/posdb_live_backup_$(date +%Y%m%d_%H%M%S).dump
docker exec pos_postgres rm -f /tmp/pre_deploy_posdb_uat.dump /tmp/pre_deploy_posdb_live.dump
```


## Network IP Assignments & Database Credentials

The system uses the following IP assignments and database settings for development and deployment configurations:
- **Development PC** (this workstation running the IDE): `192.168.1.4`
- **Ubuntu Production Server** (hosting the application containers via Docker): `192.168.1.5`
- **Development & UAT Testing Database Name**: `posdb_uat`
- **Production Live Database Name**: `posdb_live`
- **PostgreSQL Database User**: `posadmin`
</RULE[project]>
