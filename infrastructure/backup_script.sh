#!/bin/bash
# Backup Script for PostgreSQL Database

BACKUP_DIR="/var/backups/postgres"
DB_USER=${DB_USER:-"postgres"}
DB_NAME=${DB_NAME:-"PosErpDb"}
DATE=$(date +%Y%m%d_%H%M%S)
BACKUP_FILE="$BACKUP_DIR/$DB_NAME-$DATE.sql.gz"

mkdir -p $BACKUP_DIR

echo "Starting backup of $DB_NAME..."
docker exec -t erp-db-1 pg_dump -U $DB_USER $DB_NAME | gzip > $BACKUP_FILE

if [ $? -eq 0 ]; then
  echo "Backup successful: $BACKUP_FILE"
  # Optional: Upload to S3
  # aws s3 cp $BACKUP_FILE s3://my-erp-backups/
else
  echo "Backup failed!"
  exit 1
fi

# Clean up backups older than 30 days
find $BACKUP_DIR -type f -name "*.sql.gz" -mtime +30 -exec rm {} \;
echo "Old backups cleaned up."
