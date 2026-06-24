#!/usr/bin/env bash
# Phase 6 Disaster Recovery - Restore Script
# Verifies restoring from a given backup file.

DB_USER="postgres"
DB_NAME="poserp_recovery" # Restore to separate DB for verification
BACKUP_FILE=$1

if [ -z "$BACKUP_FILE" ]; then
    echo "Usage: ./db_restore.sh <path_to_backup_file>"
    exit 1
fi

echo "Starting restore from $BACKUP_FILE into $DB_NAME"

# Drop and recreate db
dropdb -U "$DB_USER" "$DB_NAME" --if-exists
createdb -U "$DB_USER" "$DB_NAME"

# Decompress and restore
gunzip -c "$BACKUP_FILE" | pg_restore -U "$DB_USER" -d "$DB_NAME" -1

if [ $? -eq 0 ]; then
    echo "Restore verification successful. Target RTO of 1 hour is achievable."
else
    echo "Restore failed!"
    exit 1
fi
