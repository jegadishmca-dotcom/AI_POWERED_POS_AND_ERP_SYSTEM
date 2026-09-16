#!/usr/bin/env bash
set -Eeuo pipefail
# ═══════════════════════════════════════════════════════════════════════
# Apple Supermarket POS & ERP System — Uninstaller
# ═══════════════════════════════════════════════════════════════════════
# Target:  Ubuntu 24.04 LTS (x86_64)
# Removes the application stack, systemd services, and host Nginx proxy.
#
# Usage:
#   sudo bash uninstall.sh                      # Standard (keeps database & backups)
#   sudo bash uninstall.sh --keep-data          # Explicit data preservation
#   sudo bash uninstall.sh --purge              # Complete purge (destroys data)
#   sudo bash uninstall.sh --dry-run            # Preview actions without changes
#   sudo bash uninstall.sh --verbose            # Detailed debug output
#   sudo bash uninstall.sh --unattended         # Non-interactive standard uninstall
#   sudo bash uninstall.sh --unattended --purge --force-data-loss
# ═══════════════════════════════════════════════════════════════════════

# ── Resolve paths ────────────────────────────────────────────────────
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPT_NAME="$(basename "${BASH_SOURCE[0]}")"

# ── Source vendor configuration ──────────────────────────────────────
VENDOR_CONF="${SCRIPT_DIR}/vendor.conf"
if [[ -f "${VENDOR_CONF}" ]]; then
    # shellcheck source=/dev/null
    source "${VENDOR_CONF}"
else
    # Fallback defaults if vendor.conf is missing
    PRODUCT_NAME="Apple Supermarket POS & ERP System"
    PRODUCT_VERSION="1.0.0"
    PRODUCT_SLUG="apple-pos"
fi

# ── Constants ────────────────────────────────────────────────────────
readonly SLUG="${PRODUCT_SLUG:-apple-pos}"
readonly INSTALL_DIR="/opt/${SLUG}"
readonly DATA_DIR="/var/lib/${SLUG}"
readonly BACKUP_DIR="${DATA_DIR}/backups"
readonly LOG_DIR="/var/log/${SLUG}"
readonly CERT_DIR="/etc/ssl/${SLUG}"
readonly SYSTEMD_DIR="/etc/systemd/system"
readonly SYSTEMD_SERVICE="${SLUG}.service"
readonly SYSTEMD_TIMER="${SLUG}-backup.timer"
readonly SYSTEMD_BACKUP_SERVICE="${SLUG}-backup.service"
readonly NGINX_AVAILABLE="/etc/nginx/sites-available/${SLUG}.conf"
readonly NGINX_ENABLED="/etc/nginx/sites-enabled/${SLUG}.conf"
readonly COMPOSE_FILE="${INSTALL_DIR}/docker-compose.yml"
readonly EMERGENCY_BACKUP_DIR="/var/backups"

# ── Colours ──────────────────────────────────────────────────────────
RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'
CYAN='\033[0;36m'; BOLD='\033[1m'; DIM='\033[2m'; NC='\033[0m'

_ts() { date '+%Y-%m-%d %H:%M:%S'; }

log_info()  { printf "${CYAN}[INFO]${NC}  %s\n" "$1"; }
log_ok()    { printf "${GREEN}[OK]${NC}    %s\n" "$1"; }
log_warn()  { printf "${YELLOW}[WARN]${NC}  %s\n" "$1"; }
log_error() { printf "${RED}[ERROR]${NC} %s\n" "$1" >&2; }
log_debug() {
    if [[ "${VERBOSE}" == "true" ]]; then
        printf "${DIM}[DEBUG] %s${NC}\n" "$1"
    fi
}

# ── Flags (mutable) ──────────────────────────────────────────────────
DRY_RUN=false
VERBOSE=false
UNATTENDED=false
PURGE=false
FORCE_DATA_LOSS=false

# ── Parse Arguments ──────────────────────────────────────────────────
parse_args() {
    while [[ $# -gt 0 ]]; do
        case "$1" in
            --dry-run)          DRY_RUN=true ;;
            --verbose)          VERBOSE=true ;;
            --unattended)       UNATTENDED=true ;;
            --keep-data)        PURGE=false ;;
            --purge)            PURGE=true ;;
            --force-data-loss)  FORCE_DATA_LOSS=true ;;
            -h|--help)
                usage
                exit 0
                ;;
            *)
                log_error "Unknown argument: $1"
                usage
                exit 1
                ;;
        esac
        shift
    done

    # Validation: unattended purge requires explicit force flag
    if [[ "${UNATTENDED}" == "true" && "${PURGE}" == "true" && "${FORCE_DATA_LOSS}" != "true" ]]; then
        log_error "--purge in --unattended mode requires --force-data-loss flag."
        exit 1
    fi
}

usage() {
    cat <<EOF
Usage: sudo bash ${SCRIPT_NAME} [OPTIONS]

Options:
  --keep-data          Preserve database, backups, and logs (DEFAULT)
  --purge              Irreversibly delete all databases, backups, and config
  --unattended         Non-interactive mode (requires --force-data-loss if --purge)
  --force-data-loss    Acknowledge permanent data destruction in unattended purge
  --dry-run            Preview actions without making changes
  --verbose            Enable debug output
  -h, --help           Show this help message

Examples:
  sudo bash ${SCRIPT_NAME}
  sudo bash ${SCRIPT_NAME} --purge
  sudo bash ${SCRIPT_NAME} --dry-run
  sudo bash ${SCRIPT_NAME} --unattended --purge --force-data-loss
EOF
}

# ── Root Check ───────────────────────────────────────────────────────
check_root() {
    if [[ "${DRY_RUN}" == "true" ]]; then
        return
    fi
    if [[ $EUID -ne 0 ]]; then
        log_error "This script must be run as root. Please use sudo."
        exit 1
    fi
}

# ── Confirmation Gate ────────────────────────────────────────────────
confirm_uninstall() {
    # Fail closed if running in a non-interactive environment without --unattended
    if [[ ! -t 0 && "${UNATTENDED}" != "true" ]]; then
        log_error "Non-interactive environment detected (stdin is not a terminal)."
        log_error "Interactive confirmation cannot be prompted."
        log_error "To run non-interactively, specify --unattended."
        if [[ "${PURGE}" == "true" ]]; then
            log_error "For --purge in non-interactive mode, you must also specify --force-data-loss:"
            log_error "    sudo bash ${SCRIPT_NAME} --unattended --purge --force-data-loss"
        fi
        exit 1
    fi

    if [[ "${DRY_RUN}" == "true" ]]; then
        log_info "[DRY RUN] Would prompt for confirmation."
        return
    fi

    if [[ "${PURGE}" == "true" ]]; then
        echo ""
        printf "${RED}${BOLD}===================================================================${NC}\n"
        printf "${RED}${BOLD} ██████  WARNING: COMPLETE DATA PURGE REQUESTED  ██████${NC}\n"
        printf "${RED}${BOLD}===================================================================${NC}\n"
        echo ""
        printf "You have specified the ${BOLD}--purge${NC} flag. This action is ${BOLD}${RED}IRREVERSIBLE${NC}.\n\n"
        printf "The following will be ${BOLD}${RED}PERMANENTLY DELETED${NC}:\n"
        printf "  ${RED}✗${NC} ALL PostgreSQL databases, sales records, invoices, and audit logs\n"
        printf "  ${RED}✗${NC} ALL local database backups in ${BACKUP_DIR}/\n"
        printf "  ${RED}✗${NC} ALL application configuration and encryption keys\n"
        printf "  ${RED}✗${NC} ALL Docker named volumes and persistent storage\n"
        printf "  ${RED}✗${NC} ALL Docker container images built for this product\n\n"
        printf "To prevent accidental data destruction, you must confirm:\n\n"

        if [[ "${UNATTENDED}" == "true" ]]; then
            log_warn "Unattended purge confirmed via --force-data-loss flag."
            return
        fi

        local confirm_slug=""
        read -r -p "Type the exact product slug '${SLUG}' to proceed with PURGE: " confirm_slug
        if [[ "${confirm_slug}" != "${SLUG}" ]]; then
            log_error "Confirmation phrase '${confirm_slug}' did not match '${SLUG}'. Purge aborted."
            exit 1
        fi
        echo ""
    else
        echo ""
        printf "${CYAN}${BOLD}===================================================================${NC}\n"
        printf "${CYAN}${BOLD} %s — Uninstaller${NC}\n" "${PRODUCT_NAME}"
        printf "${CYAN}${BOLD}===================================================================${NC}\n"
        echo ""
        printf "This will stop and remove:\n"
        printf "  - All application Docker containers (backend, frontend, postgres, pgbouncer, redis)\n"
        printf "  - Systemd services (%s, %s)\n" "${SYSTEMD_SERVICE}" "${SYSTEMD_TIMER}"
        printf "  - Host Nginx reverse proxy site configuration\n"
        printf "  - Application code in %s\n\n" "${INSTALL_DIR}"
        printf "${GREEN}${BOLD}The following DATA WILL BE PRESERVED:${NC}\n"
        printf "  ${GREEN}✓${NC} Database data and tables:  %s\n" "${DATA_DIR}/"
        printf "  ${GREEN}✓${NC} Nightly backup archives:   %s\n" "${BACKUP_DIR}/"
        printf "  ${GREEN}✓${NC} Application logs:          %s\n" "${LOG_DIR}/"
        printf "  ${GREEN}✓${NC} Configuration files:       %s\n\n" "${DATA_DIR}/config/"

        if [[ "${UNATTENDED}" == "true" ]]; then
            log_info "Unattended standard uninstall proceeding."
            return
        fi

        local confirm_std=""
        read -r -p "Are you sure you want to uninstall? (y/N): " confirm_std
        if [[ ! "${confirm_std}" =~ ^[Yy]$ ]]; then
            log_info "Uninstall aborted by user."
            exit 0
        fi
        echo ""
    fi
}

# ── Final Pre-Purge Safety Backup ────────────────────────────────────
take_pre_purge_backup() {
    if [[ "${PURGE}" != "true" ]]; then
        return
    fi

    log_info "Creating final pre-purge safety backup before data destruction..."

    if [[ "${DRY_RUN}" == "true" ]]; then
        log_info "[DRY RUN] Would take final database dump to ${EMERGENCY_BACKUP_DIR}/${SLUG}-pre-purge-TIMESTAMP.sql.gz"
        return
    fi

    mkdir -p "${EMERGENCY_BACKUP_DIR}"
    local purge_date
    purge_date="$(date +%Y%m%d_%H%M%S)"
    local emergency_backup="${EMERGENCY_BACKUP_DIR}/${SLUG}-pre-purge-${purge_date}.sql.gz"

    # Check if postgres container is running
    local pg_container="${SLUG}-postgres"
    if docker ps --format '{{.Names}}' | grep -q "^${pg_container}$"; then
        log_info "Dumping PostgreSQL database posdb_live from running container ${pg_container}..."
        if docker exec "${pg_container}" pg_dump -U posadmin -d posdb_live 2>/dev/null | gzip > "${emergency_backup}"; then
            chmod 600 "${emergency_backup}"
            log_ok "Final pre-purge safety backup saved to: ${emergency_backup}"
            log_info "This archive is located OUTSIDE the product directories and will NOT be deleted."
        else
            log_warn "pg_dump failed. Attempting direct file copy of data directory as fallback..."
            tar -czf "${emergency_backup}" -C "${DATA_DIR}" . 2>/dev/null || true
            log_warn "Fallback archive created at ${emergency_backup}"
        fi
    elif [[ -d "${DATA_DIR}" ]]; then
        log_warn "PostgreSQL container is not running. Creating tar archive of ${DATA_DIR}..."
        tar -czf "${emergency_backup}" -C "${DATA_DIR}" . 2>/dev/null || true
        log_ok "Data directory archive saved to: ${emergency_backup}"
    fi
}

# ── Stop & Disable Systemd Services ──────────────────────────────────
step_stop_systemd() {
    log_info "Stopping and removing systemd units..."

    if [[ "${DRY_RUN}" == "true" ]]; then
        log_info "[DRY RUN] Would disable and stop ${SYSTEMD_SERVICE} and ${SYSTEMD_TIMER}"
        return
    fi

    # Stop timer first
    if systemctl is-active --quiet "${SYSTEMD_TIMER}" 2>/dev/null; then
        systemctl stop "${SYSTEMD_TIMER}" || true
    fi
    if systemctl is-enabled --quiet "${SYSTEMD_TIMER}" 2>/dev/null; then
        systemctl disable "${SYSTEMD_TIMER}" || true
    fi
    rm -f "${SYSTEMD_DIR}/${SYSTEMD_TIMER}"
    rm -f "${SYSTEMD_DIR}/${SYSTEMD_BACKUP_SERVICE}"

    # Stop main service
    if systemctl is-active --quiet "${SYSTEMD_SERVICE}" 2>/dev/null; then
        systemctl stop "${SYSTEMD_SERVICE}" || true
    fi
    if systemctl is-enabled --quiet "${SYSTEMD_SERVICE}" 2>/dev/null; then
        systemctl disable "${SYSTEMD_SERVICE}" || true
    fi
    rm -f "${SYSTEMD_DIR}/${SYSTEMD_SERVICE}"

    systemctl daemon-reload
    log_ok "Systemd units removed"
}

# ── Stop Docker Containers ───────────────────────────────────────────
step_stop_docker() {
    log_info "Stopping Docker container stack..."

    if [[ "${DRY_RUN}" == "true" ]]; then
        if [[ "${PURGE}" == "true" ]]; then
            log_info "[DRY RUN] Would run: docker compose -f ${COMPOSE_FILE} down -v --remove-orphans"
        else
            log_info "[DRY RUN] Would run: docker compose -f ${COMPOSE_FILE} down --remove-orphans"
        fi
        return
    fi

    if [[ -f "${COMPOSE_FILE}" ]]; then
        cd "${INSTALL_DIR}"
        if [[ "${PURGE}" == "true" ]]; then
            docker compose down -v --remove-orphans 2>/dev/null || true
        else
            docker compose down --remove-orphans 2>/dev/null || true
        fi
    else
        # Fallback: stop containers by name pattern
        log_warn "Compose file not found at ${COMPOSE_FILE}. Stopping containers by prefix..."
        docker ps -a --format '{{.Names}}' | grep "^${SLUG}-" | while read -r cname; do
            docker stop "${cname}" 2>/dev/null || true
            docker rm -f "${cname}" 2>/dev/null || true
        done
    fi

    log_ok "Docker containers stopped and removed"
}

# ── Remove Host Nginx Proxy Configuration ────────────────────────────
step_remove_nginx() {
    log_info "Removing host Nginx reverse proxy configuration..."

    if [[ "${DRY_RUN}" == "true" ]]; then
        log_info "[DRY RUN] Would remove ${NGINX_ENABLED} and ${NGINX_AVAILABLE}"
        return
    fi

    local reload_needed=false

    if [[ -f "${NGINX_ENABLED}" || -L "${NGINX_ENABLED}" ]]; then
        rm -f "${NGINX_ENABLED}"
        reload_needed=true
    fi

    if [[ -f "${NGINX_AVAILABLE}" ]]; then
        rm -f "${NGINX_AVAILABLE}"
        reload_needed=true
    fi

    if [[ "${reload_needed}" == "true" ]]; then
        if nginx -t 2>/dev/null; then
            systemctl reload nginx 2>/dev/null || true
            log_ok "Host Nginx configuration removed and reloaded"
        else
            log_warn "Nginx configuration test failed. Please check /etc/nginx/sites-enabled manually."
        fi
    else
        log_info "No host Nginx site configuration found."
    fi
}

# ── Purge Volumes & Data (Purge Mode Only) ───────────────────────────
step_purge_data() {
    if [[ "${PURGE}" != "true" ]]; then
        return
    fi

    log_info "Purging persistent volumes, application data, certificates, and images..."

    if [[ "${DRY_RUN}" == "true" ]]; then
        log_info "[DRY RUN] Would remove volumes, images, ${DATA_DIR}, ${LOG_DIR}, and ${CERT_DIR}"
        return
    fi

    # Remove Docker named volumes
    local volumes=(
        "${SLUG}-postgres-data"
        "${SLUG}-redis-data"
        "${SLUG}-prometheus-data"
        "${SLUG}-grafana-data"
        "${SLUG}-ollama-data"
    )
    for vol in "${volumes[@]}"; do
        if docker volume inspect "${vol}" &>/dev/null; then
            docker volume rm -f "${vol}" 2>/dev/null || true
            log_debug "Removed Docker volume: ${vol}"
        fi
    done

    # Remove built application Docker images
    local images=(
        "${SLUG}-backend:${PRODUCT_VERSION}"
        "${SLUG}-frontend:${PRODUCT_VERSION}"
    )
    for img in "${images[@]}"; do
        docker rmi -f "${img}" 2>/dev/null || true
        log_debug "Removed Docker image: ${img}"
    done

    # Remove filesystem directories
    rm -rf "${DATA_DIR}"
    log_ok "Removed data directory: ${DATA_DIR}"

    rm -rf "${LOG_DIR}"
    log_ok "Removed log directory: ${LOG_DIR}"

    rm -rf "${CERT_DIR}"
    log_ok "Removed certificate directory: ${CERT_DIR}"
}

# ── Remove Application Code Directory ────────────────────────────────
step_remove_application_code() {
    log_info "Removing application code directory..."

    if [[ "${DRY_RUN}" == "true" ]]; then
        log_info "[DRY RUN] Would remove ${INSTALL_DIR}"
        return
    fi

    rm -rf "${INSTALL_DIR}"
    log_ok "Removed ${INSTALL_DIR}"
}

# ── Summary Report ───────────────────────────────────────────────────
print_summary() {
    echo ""
    printf "${BOLD}${GREEN}===================================================================${NC}\n"
    if [[ "${PURGE}" == "true" ]]; then
        printf "${BOLD}${GREEN} Uninstallation Complete (PURGED) ${NC}\n"
        printf "${BOLD}${GREEN}===================================================================${NC}\n\n"
        printf "The application stack and all associated data have been completely removed.\n\n"
        if [[ -d "${EMERGENCY_BACKUP_DIR}" ]]; then
            printf "Safety archive location:\n"
            printf "  %s/\n\n" "${EMERGENCY_BACKUP_DIR}"
        fi
    else
        printf "${BOLD}${GREEN} Uninstallation Complete (DATA PRESERVED) ${NC}\n"
        printf "${BOLD}${GREEN}===================================================================${NC}\n\n"
        printf "The application stack has been uninstalled, but your data is safe.\n\n"
        printf "${BOLD}Preserved Assets:${NC}\n"
        printf "  ✓ Database files & backups:  %s\n" "${DATA_DIR}"
        printf "  ✓ Application logs:          %s\n" "${LOG_DIR}"
        printf "  ✓ Backup archives:           %s\n\n" "${BACKUP_DIR}"
        printf "To reinstall and reconnect to this data at any time, run:\n"
        printf "  sudo bash install.sh\n\n"
    fi
}

# ── Main ─────────────────────────────────────────────────────────────
main() {
    parse_args "$@"
    check_root
    confirm_uninstall
    take_pre_purge_backup
    step_stop_systemd
    step_stop_docker
    step_remove_nginx
    step_purge_data
    step_remove_application_code
    print_summary
}

main "$@"
