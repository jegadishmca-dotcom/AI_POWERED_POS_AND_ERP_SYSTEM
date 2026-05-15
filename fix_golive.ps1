$backendDir = "d:\JEGADISH\AI_POWERED_POS_AND_ERP_SYSTEM\src\Backend"
$infraDir = "d:\JEGADISH\AI_POWERED_POS_AND_ERP_SYSTEM\infrastructure"

New-Item -ItemType Directory -Force -Path "$backendDir\PosErp.Application\Features\Audit\Services"

# 2. Audit Logging Service
@"
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Audit.Services;

public interface IAuditLoggingService
{
    Task LogActionAsync(Guid? userId, string action, string entityName, string entityId, object oldValues, object newValues, string ipAddress, CancellationToken cancellationToken);
}

public class AuditLoggingService : IAuditLoggingService
{
    private readonly IApplicationDbContext _context;

    public AuditLoggingService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task LogActionAsync(Guid? userId, string action, string entityName, string entityId, object oldValues, object newValues, string ipAddress, CancellationToken cancellationToken)
    {
        var oldJson = JsonSerializer.Serialize(oldValues);
        var newJson = JsonSerializer.Serialize(newValues);

        string sql = @"
            INSERT INTO audit_logs (user_id, action, entity_name, entity_id, old_values, new_values, ip_address)
            VALUES ({0}, {1}, {2}, {3}, CAST({4} AS JSONB), CAST({5} AS JSONB), {6})";

        await ((DbContext)_context).Database.ExecuteSqlRawAsync(sql, 
            userId, action, entityName, entityId, oldJson, newJson, ipAddress);
    }
}
"@ | Out-File -FilePath "$backendDir\PosErp.Application\Features\Audit\Services\AuditLoggingService.cs" -Encoding utf8

# 4. Backup Script (using single quotes so powershell doesn't evaluate $ variables)
@'
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
'@ | Out-File -FilePath "$infraDir\backup_script.sh" -Encoding utf8

Write-Host "Fixes applied."
