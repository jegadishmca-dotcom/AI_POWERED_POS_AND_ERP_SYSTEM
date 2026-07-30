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

## Network IP Assignments & Database Credentials

The system uses the following IP assignments and database settings for development and deployment configurations:
- **Development PC** (this workstation running the IDE): `192.168.1.4`
- **Ubuntu Production Server** (hosting the application containers via Docker): `192.168.1.5`
- **Development & UAT Testing Database Name**: `posdb_uat`
- **Production Live Database Name**: `posdb_live`
- **PostgreSQL Database User**: `posadmin`
</RULE[project]>
