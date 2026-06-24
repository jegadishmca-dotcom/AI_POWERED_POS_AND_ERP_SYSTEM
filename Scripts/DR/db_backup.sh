#!/usr/bin/env bash
# Phase 6 Disaster Recovery - Backup Script
# RPO = 15 minutes (Execute this via cron every 15 mins for critical logs)
# RTO = 1 hour

DB_USER="postgres"
DB_NAME="poserp"
BACKUP_DIR="/var/backups/poserp"
DATE=$(date +"%Y%m%d_%H%M")
FILE_NAME="$BACKUP_DIR/$DB_NAME_$DATE.sql.gz"

echo "Starting backup of $DB_NAME to $FILE_NAME"

mkdir -p "$BACKUP_DIR"
pg_dump -U "$DB_USER" -d "$DB_NAME" -F c | gzip > "$FILE_NAME"

# Verify backup size > 0
if [ -s "$FILE_NAME" ]; then
    echo "Backup successful: $FILE_NAME"
    # In production, sync to S3: aws s3 cp "$FILE_NAME" s3://my-backup-bucket/
else
    echo "Backup failed!"
    exit 1
fi
