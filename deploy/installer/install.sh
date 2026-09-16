#!/usr/bin/env bash
set -Eeuo pipefail
# ═══════════════════════════════════════════════════════════════════════
# Apple Supermarket POS & ERP System — Production Installer
# ═══════════════════════════════════════════════════════════════════════
# Target:  Ubuntu 24.04 LTS (x86_64) — fresh server install
# Deploy:  Docker Compose with host-level Nginx + TLS
#
# Usage:
#   sudo bash install.sh                          # interactive
#   sudo bash install.sh --unattended --config answers.conf
#   sudo bash install.sh --dry-run                # preview only
#   sudo bash install.sh --enable-ai              # include Ollama
#   sudo bash install.sh --no-monitoring          # skip Prometheus/Grafana
#   sudo bash install.sh --verbose                # debug output
#   sudo bash install.sh --skip-preflight         # skip hardware checks
#   sudo bash install.sh --uninstall              # remove installation
#
# Flags can be combined:
#   sudo bash install.sh --unattended --config a.conf --enable-ai --verbose
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
    echo "FATAL: vendor.conf not found at ${VENDOR_CONF}" >&2
    exit 1
fi

# ── Constants (derived from vendor.conf) ─────────────────────────────
readonly SLUG="${PRODUCT_SLUG:-apple-pos}"
readonly INSTALL_DIR="/opt/${SLUG}"
readonly DATA_DIR="/var/lib/${SLUG}"
readonly LOG_DIR="/var/log/${SLUG}"
readonly INSTALL_LOG="${LOG_DIR}/install.log"
readonly CERT_DIR="/etc/ssl/${SLUG}"
readonly BACKUP_DIR="${DATA_DIR}/backups"
readonly SYSTEMD_DIR="/etc/systemd/system"
readonly NGINX_SITE="/etc/nginx/sites-available/${SLUG}.conf"
readonly NGINX_ENABLED="/etc/nginx/sites-enabled/${SLUG}.conf"
readonly ENV_FILE="${INSTALL_DIR}/.env"
readonly COMPOSE_FILE="${INSTALL_DIR}/docker-compose.yml"
readonly COMPOSE_PROD="${SCRIPT_DIR}/docker-compose.prod.yml"

# ── Minimum system requirements ──────────────────────────────────────
readonly MIN_RAM_MB=3072          # 3 GB minimum
readonly REC_RAM_MB=8192          # 8 GB recommended
readonly AI_MIN_RAM_MB=8192       # 8 GB with Ollama
readonly AI_REC_RAM_MB=16384      # 16 GB recommended with Ollama
readonly MIN_DISK_GB=20
readonly REC_DISK_GB=50
readonly MIN_CPU=2
readonly REC_CPU=4

# ── Flags (mutable — set by argument parser) ─────────────────────────
DRY_RUN=false
VERBOSE=false
SKIP_PREFLIGHT=false
UNATTENDED=false
ENABLE_MONITORING=true
ENABLE_AI=false
CONFIG_FILE=""
DO_UNINSTALL=false

# ── Collected values (populated by prompts or config file) ───────────
COMPANY_NAME=""
ACCESS_MODE=""         # "domain" or "ip"
SERVER_FQDN=""
SERVER_IP=""
ADMIN_EMAIL=""
ADMIN_PASSWORD_INPUT=""
SYSTEM_TIMEZONE="Asia/Kolkata"
SMTP_ENABLED=false
SMTP_HOST_INPUT=""
SMTP_PORT_INPUT="587"
SMTP_FROM_INPUT=""
SMTP_PASSWORD_INPUT=""

# ── Generated secrets (populated by generate_secrets) ────────────────
GEN_POSADMIN_PASSWORD=""
GEN_JWT_SECRET=""
GEN_ADMIN_PIN=""
GEN_CASHIER_PASSWORD=""
GEN_GRAFANA_PASSWORD=""

# ── Step tracking for error handler ──────────────────────────────────
CURRENT_STEP=""
INSTALL_STARTED=false

# ═══════════════════════════════════════════════════════════════════════
#  COLOURS & LOGGING
# ═══════════════════════════════════════════════════════════════════════
RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'
CYAN='\033[0;36m'; BOLD='\033[1m'; DIM='\033[2m'; NC='\033[0m'

_ts() { date '+%Y-%m-%d %H:%M:%S'; }

_log_file() {
    if [[ -d "${LOG_DIR}" ]]; then
        echo "$(_ts) $1" >> "${INSTALL_LOG}" 2>/dev/null || true
    fi
}

log_info() {
    local msg="$1"
    printf "${CYAN}[INFO]${NC}  %s\n" "${msg}"
    _log_file "[INFO]  ${msg}"
}

log_ok() {
    local msg="$1"
    printf "${GREEN}[OK]${NC}    %s\n" "${msg}"
    _log_file "[OK]    ${msg}"
}

log_warn() {
    local msg="$1"
    printf "${YELLOW}[WARN]${NC}  %s\n" "${msg}"
    _log_file "[WARN]  ${msg}"
}

log_error() {
    local msg="$1"
    printf "${RED}[ERROR]${NC} %s\n" "${msg}" >&2
    _log_file "[ERROR] ${msg}"
}

log_step() {
    local step_num="$1" step_name="$2"
    CURRENT_STEP="${step_name}"
    echo ""
    printf "${BOLD}${CYAN}━━━ Step %s: %s ━━━${NC}\n" "${step_num}" "${step_name}"
    _log_file "━━━ Step ${step_num}: ${step_name} ━━━"
}

log_debug() {
    if [[ "${VERBOSE}" == "true" ]]; then
        local msg="$1"
        printf "${DIM}[DEBUG] %s${NC}\n" "${msg}"
        echo "$(_ts) [DEBUG] ${msg}" >> "${INSTALL_LOG}" 2>/dev/null || true
    fi
}

banner() {
    echo ""
    printf "${BOLD}${CYAN}"
    echo "╔═══════════════════════════════════════════════════════════════╗"
    printf "║  %-61s ║\n" "${PRODUCT_NAME}"
    printf "║  %-61s ║\n" "Version ${PRODUCT_VERSION} — Production Installer"
    echo "║                                                               ║"
    printf "║  %-61s ║\n" "${VENDOR_COMPANY}"
    echo "╚═══════════════════════════════════════════════════════════════╝"
    printf "${NC}\n"
}

# ═══════════════════════════════════════════════════════════════════════
#  ARGUMENT PARSING
# ═══════════════════════════════════════════════════════════════════════
parse_args() {
    while [[ $# -gt 0 ]]; do
        case "$1" in
            --dry-run)        DRY_RUN=true ;;
            --verbose)        VERBOSE=true ;;
            --skip-preflight) SKIP_PREFLIGHT=true ;;
            --unattended)     UNATTENDED=true ;;
            --no-monitoring)  ENABLE_MONITORING=false ;;
            --enable-ai)      ENABLE_AI=true ;;
            --uninstall)      DO_UNINSTALL=true ;;
            --tag)
                shift
                if [[ $# -eq 0 ]]; then
                    log_error "--tag requires a git tag argument (e.g. v1.0.0)"
                    exit 1
                fi
                RELEASE_TAG="$1"
                ;;
            --config)
                shift
                if [[ $# -eq 0 ]]; then
                    log_error "--config requires a file path argument"
                    exit 1
                fi
                CONFIG_FILE="$1"
                if [[ ! -f "${CONFIG_FILE}" ]]; then
                    log_error "Config file not found: ${CONFIG_FILE}"
                    exit 1
                fi
                ;;
            --help|-h)
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

    # Validate: --unattended requires --config
    if [[ "${UNATTENDED}" == "true" && -z "${CONFIG_FILE}" ]]; then
        log_error "--unattended requires --config <file>"
        exit 1
    fi
}

usage() {
    cat <<EOF
Usage: sudo bash ${SCRIPT_NAME} [OPTIONS]

Options:
  --dry-run          Preview actions without making changes
  --verbose          Enable debug output
  --skip-preflight   Skip hardware and OS pre-flight checks
  --unattended       Non-interactive mode (requires --config)
  --config <file>    Load answers from config file
  --tag <tag>        Deploy a specific git release tag (overrides vendor.conf)
  --no-monitoring    Skip Prometheus + Grafana installation
  --enable-ai        Include Ollama AI Co-Pilot
  --uninstall        Remove the installation (see also: uninstall.sh)
  -h, --help         Show this help message

Examples:
  sudo bash ${SCRIPT_NAME}
  sudo bash ${SCRIPT_NAME} --tag v1.0.0
  sudo bash ${SCRIPT_NAME} --unattended --config answers.conf
  sudo bash ${SCRIPT_NAME} --dry-run --verbose
  sudo bash ${SCRIPT_NAME} --enable-ai --no-monitoring

Full documentation: see INSTALLATION_GUIDE.md
Support: ${VENDOR_SUPPORT_EMAIL} | ${VENDOR_SUPPORT_PHONE}
EOF
}

# ═══════════════════════════════════════════════════════════════════════
#  ERROR HANDLING & ROLLBACK
# ═══════════════════════════════════════════════════════════════════════
on_error() {
    local lineno="$1"
    echo ""
    log_error "═══════════════════════════════════════════════════════════"
    log_error "Installation FAILED at line ${lineno}"
    [[ -n "${CURRENT_STEP}" ]] && log_error "During step: ${CURRENT_STEP}"
    log_error ""
    log_error "Full log: ${INSTALL_LOG}"
    log_error ""
    log_error "To retry, fix the issue and re-run: sudo bash ${SCRIPT_DIR}/${SCRIPT_NAME}"
    log_error "To rollback a partial install:      sudo bash ${SCRIPT_DIR}/${SCRIPT_NAME} --uninstall"
    log_error ""
    log_error "Support: ${VENDOR_SUPPORT_EMAIL} | ${VENDOR_SUPPORT_PHONE}"
    log_error "═══════════════════════════════════════════════════════════"
}
trap 'on_error ${LINENO}' ERR

rollback() {
    log_warn "Rolling back partial installation..."

    # Stop containers if running
    if [[ -f "${COMPOSE_FILE}" ]]; then
        docker compose -f "${COMPOSE_FILE}" --project-name "${SLUG}" down 2>/dev/null || true
    fi

    # Disable systemd units
    systemctl disable "${SLUG}.service" 2>/dev/null || true
    systemctl disable "${SLUG}-backup.timer" 2>/dev/null || true
    rm -f "${SYSTEMD_DIR}/${SLUG}.service" \
          "${SYSTEMD_DIR}/${SLUG}-backup.service" \
          "${SYSTEMD_DIR}/${SLUG}-backup.timer" 2>/dev/null || true
    systemctl daemon-reload 2>/dev/null || true

    # Remove host nginx config
    rm -f "${NGINX_ENABLED}" "${NGINX_SITE}" 2>/dev/null || true
    systemctl reload nginx 2>/dev/null || true

    log_warn "Rollback complete. Data volumes preserved."
    log_warn "To fully purge data: sudo bash uninstall.sh --purge"
}

# ═══════════════════════════════════════════════════════════════════════
#  PRE-FLIGHT CHECKS
# ═══════════════════════════════════════════════════════════════════════
preflight_check_root() {
    if [[ "${DRY_RUN}" == "true" ]]; then
        log_info "[DRY RUN] Would check for root (skipped in dry-run)"
        return
    fi
    if [[ "${EUID}" -ne 0 ]]; then
        log_error "This installer must be run as root (use sudo)."
        exit 1
    fi
    log_ok "Running as root"
}

preflight_check_os() {
    if [[ ! -f /etc/os-release ]]; then
        log_error "Cannot detect OS: /etc/os-release not found"
        exit 1
    fi
    # shellcheck source=/dev/null
    source /etc/os-release

    if [[ "${ID}" != "ubuntu" ]]; then
        log_error "Unsupported OS: ${ID}. This installer requires Ubuntu."
        exit 1
    fi

    local major_ver="${VERSION_ID%%.*}"
    if [[ "${major_ver}" -lt 22 ]]; then
        log_error "Ubuntu ${VERSION_ID} is too old. Minimum: 22.04 LTS"
        exit 1
    fi

    if [[ "${VERSION_ID}" == "24.04" ]]; then
        log_ok "OS: Ubuntu ${VERSION_ID} LTS (${PRETTY_NAME})"
    elif [[ "${VERSION_ID}" == "22.04" ]]; then
        log_warn "OS: Ubuntu ${VERSION_ID} LTS — supported but 24.04 LTS is recommended"
    else
        log_warn "OS: Ubuntu ${VERSION_ID} — not officially tested; proceed with caution"
    fi
}

preflight_check_arch() {
    local arch
    arch="$(uname -m)"
    if [[ "${arch}" != "x86_64" ]]; then
        log_error "Unsupported architecture: ${arch}. This installer requires x86_64."
        exit 1
    fi
    log_ok "Architecture: ${arch}"
}

preflight_check_ram() {
    local total_kb total_mb
    total_kb=$(grep MemTotal /proc/meminfo | awk '{print $2}')
    total_mb=$((total_kb / 1024))

    local min_required="${MIN_RAM_MB}"
    local recommended="${REC_RAM_MB}"
    if [[ "${ENABLE_AI}" == "true" ]]; then
        min_required="${AI_MIN_RAM_MB}"
        recommended="${AI_REC_RAM_MB}"
    fi

    if [[ ${total_mb} -lt ${min_required} ]]; then
        log_error "Insufficient RAM: ${total_mb} MB. Minimum required: ${min_required} MB"
        if [[ "${ENABLE_AI}" == "true" ]]; then
            log_error "AI features require at least ${AI_MIN_RAM_MB} MB RAM"
        fi
        exit 1
    elif [[ ${total_mb} -lt ${recommended} ]]; then
        log_warn "RAM: ${total_mb} MB (minimum met; recommended: ${recommended} MB)"
    else
        log_ok "RAM: ${total_mb} MB (meets recommended: ${recommended} MB)"
    fi
}

preflight_check_disk() {
    local avail_gb
    avail_gb=$(df -BG --output=avail /opt 2>/dev/null | tail -1 | tr -d '[:space:]G')
    if [[ -z "${avail_gb}" ]]; then
        avail_gb=$(df -BG --output=avail / | tail -1 | tr -d '[:space:]G')
    fi

    if [[ ${avail_gb} -lt ${MIN_DISK_GB} ]]; then
        log_error "Insufficient disk space: ${avail_gb} GB available. Minimum: ${MIN_DISK_GB} GB"
        exit 1
    elif [[ ${avail_gb} -lt ${REC_DISK_GB} ]]; then
        log_warn "Disk space: ${avail_gb} GB available (recommended: ${REC_DISK_GB} GB)"
    else
        log_ok "Disk space: ${avail_gb} GB available"
    fi
}

preflight_check_cpu() {
    local cores
    cores=$(nproc 2>/dev/null || echo 1)

    if [[ ${cores} -lt ${MIN_CPU} ]]; then
        log_error "Insufficient CPU cores: ${cores}. Minimum required: ${MIN_CPU}"
        exit 1
    elif [[ ${cores} -lt ${REC_CPU} ]]; then
        log_warn "CPU cores: ${cores} (recommended: ${REC_CPU})"
    else
        log_ok "CPU cores: ${cores}"
    fi
}

preflight_check_ports() {
    local blocked=false
    for port in 80 443; do
        if ss -tlnp 2>/dev/null | grep -q ":${port} "; then
            local proc
            proc=$(ss -tlnp 2>/dev/null | grep ":${port} " | awk '{print $NF}' | head -1)
            log_warn "Port ${port} is already in use by: ${proc}"
            blocked=true
        fi
    done

    if [[ "${blocked}" == "true" ]]; then
        log_warn "Ports 80/443 are required. The installer will configure Nginx to use them."
        log_warn "Existing services on these ports will be stopped/replaced."
    else
        log_ok "Ports 80 and 443 are available"
    fi
}

preflight_check_internet() {
    local test_urls=("https://download.docker.com" "https://github.com")
    local reachable=true

    for url in "${test_urls[@]}"; do
        if ! curl -sf --max-time 10 -o /dev/null "${url}" 2>/dev/null; then
            log_warn "Cannot reach ${url}"
            reachable=false
        fi
    done

    if [[ "${reachable}" == "true" ]]; then
        log_ok "Internet connectivity verified"
    else
        log_error "Internet access is required to download Docker images and packages."
        log_error "Ensure firewall/proxy allows HTTPS access to download.docker.com and github.com"
        exit 1
    fi
}

preflight_check_existing() {
    if [[ -d "${INSTALL_DIR}" && -f "${ENV_FILE}" ]]; then
        log_warn "Existing installation detected at ${INSTALL_DIR}"
        log_warn "The installer will preserve your data and update the application."
        if [[ "${UNATTENDED}" != "true" ]]; then
            read -rp "Continue with upgrade? [y/N]: " confirm
            if [[ "${confirm}" != "y" && "${confirm}" != "Y" ]]; then
                log_info "Installation cancelled by user."
                exit 0
            fi
        fi
    fi
}

run_preflight() {
    log_step "1" "Pre-flight Checks"

    if [[ "${SKIP_PREFLIGHT}" == "true" ]]; then
        log_warn "Pre-flight checks SKIPPED (--skip-preflight flag)"
        preflight_check_root  # root check is never skippable
        return
    fi

    preflight_check_root
    preflight_check_os
    preflight_check_arch
    preflight_check_ram
    preflight_check_disk
    preflight_check_cpu
    preflight_check_ports
    preflight_check_internet
    preflight_check_existing

    echo ""
    log_ok "All pre-flight checks passed"
}

# ═══════════════════════════════════════════════════════════════════════
#  INTERACTIVE PROMPTS
# ═══════════════════════════════════════════════════════════════════════
prompt_value() {
    # Usage: prompt_value "Prompt text" DEFAULT_VALUE VARIABLE_NAME [--secret]
    local prompt="$1" default="$2" varname="$3" secret="${4:-}"
    local value

    if [[ "${UNATTENDED}" == "true" ]]; then
        # In unattended mode, use existing value or default
        value="${!varname:-${default}}"
    else
        if [[ -n "${default}" ]]; then
            prompt="${prompt} [${default}]"
        fi
        if [[ "${secret}" == "--secret" ]]; then
            read -rsp "${prompt}: " value
            echo ""
        else
            read -rp "${prompt}: " value
        fi
        value="${value:-${default}}"
    fi

    # Export the variable
    printf -v "${varname}" '%s' "${value}"
}

prompt_company() {
    prompt_value "Company / store name" "Apple Supermarket" COMPANY_NAME
    if [[ -z "${COMPANY_NAME}" ]]; then
        log_error "Company name is required."
        exit 1
    fi
    log_ok "Company: ${COMPANY_NAME}"
}

prompt_hostname() {
    echo ""
    log_info "How will users access this server?"
    echo "  1) Domain name  (e.g., pos.mystore.com — best for HTTPS)"
    echo "  2) LAN IP address  (e.g., 192.168.1.5 — common for on-premise)"

    if [[ "${UNATTENDED}" == "true" ]]; then
        ACCESS_MODE="${ACCESS_MODE:-ip}"
    else
        local choice
        read -rp "Select [1/2] (default: 2): " choice
        case "${choice}" in
            1) ACCESS_MODE="domain" ;;
            *) ACCESS_MODE="ip" ;;
        esac
    fi

    local detected_ip
    detected_ip="$(hostname -I 2>/dev/null | awk '{print $1}' || true)"
    if [[ -z "${detected_ip}" ]]; then
        detected_ip="$(ip -4 addr show scope global 2>/dev/null | grep -oP '(?<=inet\s)\d+(\.\d+){3}' | head -n 1 || true)"
    fi
    [[ -z "${detected_ip}" ]] && detected_ip="127.0.0.1"

    if [[ "${ACCESS_MODE}" == "domain" ]]; then
        prompt_value "Enter the fully qualified domain name (FQDN)" "" SERVER_FQDN
        if [[ -z "${SERVER_FQDN}" ]]; then
            log_error "FQDN is required when using domain access mode."
            exit 1
        fi
        SERVER_IP="${detected_ip}"
        log_ok "Domain: ${SERVER_FQDN} (server IP: ${SERVER_IP})"
    else
        SERVER_IP="${detected_ip}"
        SERVER_FQDN="${SERVER_IP}"
        log_ok "LAN IP: ${SERVER_IP}"
    fi
}

prompt_admin() {
    echo ""
    prompt_value "Admin email address" "admin@${COMPANY_NAME// /-}.local" ADMIN_EMAIL
    if [[ -z "${ADMIN_EMAIL}" ]]; then
        log_error "Admin email is required."
        exit 1
    fi

    if [[ "${UNATTENDED}" == "true" ]]; then
        ADMIN_PASSWORD_INPUT="${ADMIN_PASSWORD_INPUT:-${ADMIN_PASSWORD:-}}"
    else
        local pass1 pass2
        while true; do
            read -rsp "Admin password (min 12 chars, must include uppercase, lowercase, digit, special): " pass1
            echo ""
            if [[ ${#pass1} -lt 12 ]]; then
                log_warn "Password too short (minimum 12 characters). Try again."
                continue
            fi
            read -rsp "Confirm admin password: " pass2
            echo ""
            if [[ "${pass1}" != "${pass2}" ]]; then
                log_warn "Passwords do not match. Try again."
                continue
            fi
            ADMIN_PASSWORD_INPUT="${pass1}"
            break
        done
    fi

    if [[ -z "${ADMIN_PASSWORD_INPUT}" ]]; then
        log_error "Admin password is required."
        exit 1
    fi
    log_ok "Admin account configured: ${ADMIN_EMAIL}"
}

prompt_timezone() {
    echo ""
    prompt_value "Timezone" "Asia/Kolkata" SYSTEM_TIMEZONE
    # Validate timezone
    if [[ ! -f "/usr/share/zoneinfo/${SYSTEM_TIMEZONE}" ]]; then
        log_warn "Timezone '${SYSTEM_TIMEZONE}' not found in zoneinfo database. Using Asia/Kolkata."
        SYSTEM_TIMEZONE="Asia/Kolkata"
    fi
    log_ok "Timezone: ${SYSTEM_TIMEZONE}"
}

prompt_smtp() {
    echo ""
    log_info "Email configuration (for reports, alerts, password resets)"

    if [[ "${UNATTENDED}" == "true" ]]; then
        SMTP_ENABLED="${SMTP_ENABLED:-false}"
    else
        read -rp "Configure SMTP email now? [y/N]: " smtp_choice
        if [[ "${smtp_choice}" == "y" || "${smtp_choice}" == "Y" ]]; then
            SMTP_ENABLED=true
        else
            SMTP_ENABLED=false
        fi
    fi

    if [[ "${SMTP_ENABLED}" == "true" ]]; then
        prompt_value "SMTP host" "" SMTP_HOST_INPUT
        prompt_value "SMTP port" "587" SMTP_PORT_INPUT
        prompt_value "From email address" "" SMTP_FROM_INPUT
        prompt_value "SMTP password" "" SMTP_PASSWORD_INPUT --secret
        log_ok "SMTP configured: ${SMTP_HOST_INPUT}:${SMTP_PORT_INPUT}"
    else
        SMTP_HOST_INPUT=""
        SMTP_PORT_INPUT="587"
        SMTP_FROM_INPUT=""
        SMTP_PASSWORD_INPUT=""
        log_info "SMTP skipped — email features will be disabled"
    fi
}

prompt_monitoring() {
    echo ""
    if [[ "${UNATTENDED}" != "true" && "${ENABLE_MONITORING}" == "true" ]]; then
        read -rp "Install monitoring stack (Prometheus + Grafana)? [Y/n]: " mon_choice
        if [[ "${mon_choice}" == "n" || "${mon_choice}" == "N" ]]; then
            ENABLE_MONITORING=false
        fi
    fi

    if [[ "${ENABLE_MONITORING}" == "true" ]]; then
        log_ok "Monitoring: Prometheus + Grafana (localhost access via SSH tunnel)"
    else
        log_info "Monitoring: Skipped"
    fi
}

prompt_ai() {
    echo ""
    if [[ "${UNATTENDED}" != "true" && "${ENABLE_AI}" != "true" ]]; then
        log_info "AI Co-Pilot (optional): provides intelligent business insights"
        log_warn "Requires: ~4 GB download, 4+ GB RAM for the language model"
        read -rp "Enable AI Co-Pilot? [y/N]: " ai_choice
        if [[ "${ai_choice}" == "y" || "${ai_choice}" == "Y" ]]; then
            ENABLE_AI=true
        fi
    fi

    if [[ "${ENABLE_AI}" == "true" ]]; then
        log_ok "AI Co-Pilot: Enabled (Ollama will be installed)"
    else
        log_info "AI Co-Pilot: Disabled (can be enabled later with: sudo bash install.sh --enable-ai)"
    fi
}

load_config_file() {
    if [[ -n "${CONFIG_FILE}" && -f "${CONFIG_FILE}" ]]; then
        log_info "Loading configuration from: ${CONFIG_FILE}"
        # shellcheck source=/dev/null
        source "${CONFIG_FILE}"
        # Map config file variables to internal variables
        ADMIN_PASSWORD_INPUT="${ADMIN_PASSWORD:-}"
        SMTP_HOST_INPUT="${SMTP_HOST:-}"
        SMTP_PORT_INPUT="${SMTP_PORT:-587}"
        SMTP_FROM_INPUT="${SMTP_FROM:-}"
        SMTP_PASSWORD_INPUT="${SMTP_PASSWORD:-}"
    fi
}

run_prompts() {
    log_step "2" "Installation Configuration"

    load_config_file

    prompt_company
    prompt_hostname
    prompt_admin
    prompt_timezone
    prompt_smtp
    prompt_monitoring
    prompt_ai

    echo ""
    log_info "━━━ Configuration Summary ━━━"
    log_info "  Company:      ${COMPANY_NAME}"
    log_info "  Access:       ${ACCESS_MODE} (${SERVER_FQDN})"
    log_info "  Admin:        ${ADMIN_EMAIL}"
    log_info "  Timezone:     ${SYSTEM_TIMEZONE}"
    log_info "  SMTP:         $(if [[ "${SMTP_ENABLED}" == "true" ]]; then echo "${SMTP_HOST_INPUT}:${SMTP_PORT_INPUT}"; else echo "Disabled"; fi)"
    log_info "  Monitoring:   $(if [[ "${ENABLE_MONITORING}" == "true" ]]; then echo "Yes"; else echo "No"; fi)"
    log_info "  AI Co-Pilot:  $(if [[ "${ENABLE_AI}" == "true" ]]; then echo "Yes"; else echo "No"; fi)"
    log_info "  Install dir:  ${INSTALL_DIR}"
    echo ""

    if [[ "${UNATTENDED}" != "true" && "${DRY_RUN}" != "true" ]]; then
        read -rp "Proceed with installation? [Y/n]: " final_confirm
        if [[ "${final_confirm}" == "n" || "${final_confirm}" == "N" ]]; then
            log_info "Installation cancelled by user."
            exit 0
        fi
    fi
}

# ═══════════════════════════════════════════════════════════════════════
#  SECRET GENERATION
# ═══════════════════════════════════════════════════════════════════════
generate_password() {
    # Generate a cryptographically strong random password
    local length="${1:-32}"
    openssl rand -base64 48 | tr -d '/+=' | head -c "${length}"
}

generate_pin() {
    # Generate a 4-digit numeric PIN
    printf '%04d' "$(( RANDOM % 10000 ))"
}

generate_secrets() {
    log_info "Generating cryptographic secrets..."
    GEN_POSADMIN_PASSWORD="$(generate_password 32)"
    GEN_JWT_SECRET="$(generate_password 64)"
    GEN_ADMIN_PIN="$(generate_pin)"
    GEN_CASHIER_PASSWORD="$(generate_password 20)"
    GEN_GRAFANA_PASSWORD="$(generate_password 24)"
    log_ok "Secrets generated (will be written to ${ENV_FILE} with 600 permissions)"
}

# ═══════════════════════════════════════════════════════════════════════
#  INSTALLATION STEPS
# ═══════════════════════════════════════════════════════════════════════

# ── Step 3: System Update & Dependencies ─────────────────────────────
step_system_update() {
    log_step "3" "System Update & Dependencies"

    if [[ "${DRY_RUN}" == "true" ]]; then
        log_info "[DRY RUN] Would run: apt-get update && apt-get upgrade"
        log_info "[DRY RUN] Would install: curl, git, openssl, ufw, nginx, certbot"
        return
    fi

    export DEBIAN_FRONTEND=noninteractive

    log_info "Updating package index..."
    apt-get update -qq

    log_info "Installing system dependencies..."
    apt-get install -y -qq \
        apt-transport-https \
        ca-certificates \
        curl \
        gnupg \
        lsb-release \
        git \
        openssl \
        ufw \
        nginx \
        certbot \
        python3-certbot-nginx \
        jq \
        unzip \
        > /dev/null 2>&1

    # Set timezone
    log_info "Setting timezone to ${SYSTEM_TIMEZONE}..."
    timedatectl set-timezone "${SYSTEM_TIMEZONE}" 2>/dev/null || \
        ln -sf "/usr/share/zoneinfo/${SYSTEM_TIMEZONE}" /etc/localtime

    log_ok "System dependencies installed"
}

# ── Step 4: Install Docker Engine ────────────────────────────────────
step_install_docker() {
    log_step "4" "Docker Engine & Compose Plugin"

    if [[ "${DRY_RUN}" == "true" ]]; then
        log_info "[DRY RUN] Would install Docker Engine from official apt repository"
        return
    fi

    # Check if Docker is already installed
    if command -v docker &>/dev/null; then
        local docker_ver
        docker_ver=$(docker --version 2>/dev/null | grep -oP '\d+\.\d+\.\d+' || echo "unknown")
        log_ok "Docker already installed: v${docker_ver}"

        # Verify Compose plugin
        if docker compose version &>/dev/null; then
            local compose_ver
            compose_ver=$(docker compose version --short 2>/dev/null || echo "unknown")
            log_ok "Docker Compose plugin: v${compose_ver}"
            return
        else
            log_warn "Docker Compose plugin not found. Installing..."
        fi
    fi

    log_info "Installing Docker Engine from official repository..."

    # Remove any conflicting packages
    for pkg in docker.io docker-doc docker-compose podman-docker containerd runc; do
        apt-get remove -y "${pkg}" 2>/dev/null || true
    done

    # Add Docker's official GPG key
    install -m 0755 -d /etc/apt/keyrings
    curl -fsSL https://download.docker.com/linux/ubuntu/gpg \
        -o /etc/apt/keyrings/docker.asc
    chmod a+r /etc/apt/keyrings/docker.asc

    # Add Docker repository
    echo \
        "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] \
        https://download.docker.com/linux/ubuntu \
        $(. /etc/os-release && echo "${VERSION_CODENAME}") stable" \
        > /etc/apt/sources.list.d/docker.list

    apt-get update -qq

    # Install Docker Engine, CLI, Compose plugin, and Buildx
    apt-get install -y -qq \
        docker-ce \
        docker-ce-cli \
        containerd.io \
        docker-buildx-plugin \
        docker-compose-plugin \
        > /dev/null 2>&1

    # Enable and start Docker
    systemctl enable docker
    systemctl start docker

    # Verify
    docker --version
    docker compose version

    log_ok "Docker Engine and Compose plugin installed"
}

# ── Step 5: Create User & Directory Structure ────────────────────────
step_create_dirs() {
    log_step "5" "Directory Structure & Service User"

    if [[ "${DRY_RUN}" == "true" ]]; then
        log_info "[DRY RUN] Would create: ${INSTALL_DIR}, ${DATA_DIR}, ${LOG_DIR}"
        return
    fi

    # Create directories
    mkdir -p "${INSTALL_DIR}"
    mkdir -p "${DATA_DIR}"
    mkdir -p "${DATA_DIR}/config"
    mkdir -p "${BACKUP_DIR}"
    mkdir -p "${LOG_DIR}"
    mkdir -p "${CERT_DIR}"

    # Set permissions
    chmod 755 "${INSTALL_DIR}"
    chmod 750 "${DATA_DIR}"
    chmod 750 "${DATA_DIR}/config"
    chmod 750 "${BACKUP_DIR}"
    chmod 755 "${LOG_DIR}"
    chmod 700 "${CERT_DIR}"

    # Initialize operation_mode.json to LIVE if not already present
    if [[ ! -f "${DATA_DIR}/config/operation_mode.json" ]]; then
        cat > "${DATA_DIR}/config/operation_mode.json" <<'EOF'
{
  "ActiveMode": "LIVE",
  "TokenVersion": 1
}
EOF
        chmod 640 "${DATA_DIR}/config/operation_mode.json"
    fi

    # Initialize install log if not already present
    touch "${INSTALL_LOG}"
    chmod 640 "${INSTALL_LOG}"

    log_ok "Directories created"
    log_debug "  INSTALL_DIR: ${INSTALL_DIR}"
    log_debug "  DATA_DIR:    ${DATA_DIR}"
    log_debug "  BACKUP_DIR:  ${BACKUP_DIR}"
    log_debug "  LOG_DIR:     ${LOG_DIR}"
    log_debug "  CERT_DIR:    ${CERT_DIR}"
}

# ── Step 6: Clone Application ───────────────────────────────────────
step_deploy_application() {
    log_step "6" "Deploy Application Code"

    if [[ "${DRY_RUN}" == "true" ]]; then
        log_info "[DRY RUN] Would clone ${REPO_URL} at tag ${RELEASE_TAG} to ${INSTALL_DIR}"
        return
    fi

    if [[ -d "${INSTALL_DIR}/.git" ]]; then
        log_info "Existing repository found at ${INSTALL_DIR}. Fetching tags..."
        cd "${INSTALL_DIR}"
        git fetch --tags --force
        if ! git checkout "${RELEASE_TAG}" 2>/dev/null; then
            log_error "Git release tag '${RELEASE_TAG}' not found in ${INSTALL_DIR}."
            log_error "Production installations MUST use an immutable release tag."
            log_error "To tag the release in git:  git tag -a ${RELEASE_TAG} -m 'Release ${RELEASE_TAG}' && git push origin ${RELEASE_TAG}"
            log_error "To deploy a different tag:   sudo bash ${SCRIPT_NAME} --tag <tag>"
            exit 1
        fi
    else
        log_info "Cloning repository at release tag '${RELEASE_TAG}'..."
        if ! git clone --depth 1 --branch "${RELEASE_TAG}" "${REPO_URL}" "${INSTALL_DIR}" 2>/dev/null; then
            log_error "Failed to clone repository at release tag '${RELEASE_TAG}' from ${REPO_URL}."
            log_error "The release tag '${RELEASE_TAG}' does not exist on the remote repository."
            log_error "Production releases require a published git tag. Create the tag before running install.sh:"
            log_error "    git tag -a ${RELEASE_TAG} -m 'Release ${RELEASE_TAG}' && git push origin ${RELEASE_TAG}"
            log_error "To deploy an existing tag: sudo bash ${SCRIPT_NAME} --tag <tag>"
            exit 1
        fi
    fi

    # Copy production compose file
    cp "${COMPOSE_PROD}" "${COMPOSE_FILE}"
    log_ok "Application code deployed to ${INSTALL_DIR}"
}

# ── Step 7: Generate .env ────────────────────────────────────────────
step_generate_env() {
    log_step "7" "Generate Environment Configuration"

    if [[ "${DRY_RUN}" == "true" ]]; then
        log_info "[DRY RUN] Would generate ${ENV_FILE} with secrets"
        return
    fi

    generate_secrets

    # Build profiles string
    local profiles=""
    if [[ "${ENABLE_MONITORING}" == "true" ]]; then
        profiles="monitoring"
    fi
    if [[ "${ENABLE_AI}" == "true" ]]; then
        [[ -n "${profiles}" ]] && profiles="${profiles},"
        profiles="${profiles}ai"
    fi

    # Read template and substitute
    local env_template="${SCRIPT_DIR}/.env.template"
    if [[ ! -f "${env_template}" ]]; then
        log_error ".env.template not found at ${env_template}"
        exit 1
    fi

    local env_content
    env_content="$(cat "${env_template}")"

    # If an existing .env has a POSADMIN_PASSWORD, preserve it (data safety)
    local existing_db_pass=""
    if [[ -f "${ENV_FILE}" ]]; then
        existing_db_pass="$(grep -oP '^POSADMIN_PASSWORD=\K.*' "${ENV_FILE}" 2>/dev/null || true)"
    fi
    if [[ -n "${existing_db_pass}" ]]; then
        log_warn "Preserving existing database password (data safety)"
        GEN_POSADMIN_PASSWORD="${existing_db_pass}"
    fi

    # Substitute all placeholders
    env_content="${env_content//__PRODUCT_VERSION__/${PRODUCT_VERSION}}"
    env_content="${env_content//__INSTALL_DIR__/${INSTALL_DIR}}"
    env_content="${env_content//__DATA_DIR__/${DATA_DIR}}"
    env_content="${env_content//__POSADMIN_PASSWORD__/${GEN_POSADMIN_PASSWORD}}"
    env_content="${env_content//__JWT_SECRET__/${GEN_JWT_SECRET}}"
    env_content="${env_content//__ADMIN_PASSWORD__/${ADMIN_PASSWORD_INPUT}}"
    env_content="${env_content//__ADMIN_PIN__/${GEN_ADMIN_PIN}}"
    env_content="${env_content//__CASHIER_PASSWORD__/${GEN_CASHIER_PASSWORD}}"
    env_content="${env_content//__ADMIN_EMAIL__/${ADMIN_EMAIL}}"
    env_content="${env_content//__SMTP_HOST__/${SMTP_HOST_INPUT}}"
    env_content="${env_content//__SMTP_PORT__/${SMTP_PORT_INPUT}}"
    env_content="${env_content//__SMTP_FROM__/${SMTP_FROM_INPUT}}"
    env_content="${env_content//__SMTP_PASSWORD__/${SMTP_PASSWORD_INPUT}}"
    env_content="${env_content//__SYSTEM_TIMEZONE__/${SYSTEM_TIMEZONE}}"
    env_content="${env_content//__SERVER_HOSTNAME__/${SERVER_FQDN}}"
    env_content="${env_content//__GRAFANA_PASSWORD__/${GEN_GRAFANA_PASSWORD}}"
    env_content="${env_content//__COMPOSE_PROFILES__/${profiles}}"

    # Write with strict permissions — root-only
    printf '%s\n' "${env_content}" > "${ENV_FILE}"
    chmod 600 "${ENV_FILE}"
    chown root:root "${ENV_FILE}"

    log_ok ".env generated at ${ENV_FILE} (permissions: 600, owner: root)"
}

# ── Step 8: TLS Certificate Setup ───────────────────────────────────
step_configure_tls() {
    log_step "8" "TLS Certificate Configuration"

    if [[ "${DRY_RUN}" == "true" ]]; then
        log_info "[DRY RUN] Would configure TLS for ${SERVER_FQDN}"
        return
    fi

    local cert_path="${CERT_DIR}/fullchain.pem"
    local key_path="${CERT_DIR}/privkey.pem"

    if [[ "${ACCESS_MODE}" == "domain" ]]; then
        log_info "Attempting Let's Encrypt certificate for ${SERVER_FQDN}..."

        # Ensure Nginx is running for ACME challenge
        systemctl start nginx 2>/dev/null || true

        if certbot certonly \
            --nginx \
            --non-interactive \
            --agree-tos \
            --email "${ADMIN_EMAIL}" \
            -d "${SERVER_FQDN}" \
            2>/dev/null; then

            # Symlink Let's Encrypt certs
            ln -sf "/etc/letsencrypt/live/${SERVER_FQDN}/fullchain.pem" "${cert_path}"
            ln -sf "/etc/letsencrypt/live/${SERVER_FQDN}/privkey.pem" "${key_path}"
            log_ok "Let's Encrypt certificate issued for ${SERVER_FQDN}"
            return
        else
            log_warn "Let's Encrypt failed (common on-premise — port 80 may not be internet-reachable)"
            log_warn "Falling back to self-signed certificate..."
        fi
    fi

    # Self-signed certificate (domain or IP fallback)
    log_info "Generating self-signed certificate (valid 3650 days)..."

    local san_entry
    if [[ "${ACCESS_MODE}" == "domain" ]]; then
        san_entry="DNS:${SERVER_FQDN},IP:${SERVER_IP}"
    else
        san_entry="IP:${SERVER_IP}"
    fi

    openssl req -x509 -nodes -days 3650 \
        -newkey rsa:2048 \
        -keyout "${key_path}" \
        -out "${cert_path}" \
        -subj "/C=IN/ST=Tamil Nadu/L=Chennai/O=${COMPANY_NAME}/CN=${SERVER_FQDN}" \
        -addext "subjectAltName=${san_entry}" \
        2>/dev/null

    chmod 600 "${key_path}"
    chmod 644 "${cert_path}"

    log_ok "Self-signed certificate generated"
    log_warn "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    log_warn "SELF-SIGNED CERTIFICATE: Browsers will show a security warning."
    log_warn "To avoid this on POS terminals, import ${cert_path}"
    log_warn "into each browser's trusted certificate store."
    log_warn "See INSTALLATION_GUIDE.md §12 for instructions."
    log_warn "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
}

# ── Step 9: Host Nginx Configuration ────────────────────────────────
step_configure_host_nginx() {
    log_step "9" "Host Nginx Reverse Proxy"

    if [[ "${DRY_RUN}" == "true" ]]; then
        log_info "[DRY RUN] Would configure Nginx at ${NGINX_SITE}"
        return
    fi

    local nginx_template="${SCRIPT_DIR}/apple-pos-nginx.conf.template"
    if [[ ! -f "${nginx_template}" ]]; then
        log_error "Nginx template not found: ${nginx_template}"
        exit 1
    fi

    # Read template and substitute
    local nginx_content
    nginx_content="$(cat "${nginx_template}")"
    nginx_content="${nginx_content//__SERVER_NAME__/${SERVER_FQDN}}"
    nginx_content="${nginx_content//__SSL_CERT_PATH__/${CERT_DIR}/fullchain.pem}"
    nginx_content="${nginx_content//__SSL_KEY_PATH__/${CERT_DIR}/privkey.pem}"

    # Write config
    printf '%s\n' "${nginx_content}" > "${NGINX_SITE}"

    # Disable default site, enable ours
    rm -f /etc/nginx/sites-enabled/default 2>/dev/null || true
    ln -sf "${NGINX_SITE}" "${NGINX_ENABLED}"

    # Test and reload
    if nginx -t 2>/dev/null; then
        systemctl reload nginx
        log_ok "Nginx configured: ${SERVER_FQDN} → 127.0.0.1:8000"
    else
        log_error "Nginx configuration test failed!"
        nginx -t
        exit 1
    fi
}

# ── Step 10: UFW Firewall ───────────────────────────────────────────
step_configure_ufw() {
    log_step "10" "UFW Firewall Rules"

    if [[ "${DRY_RUN}" == "true" ]]; then
        log_info "[DRY RUN] Would configure UFW: allow 22/tcp, 80/tcp, 443/tcp; deny all other inbound"
        return
    fi

    # Reset UFW to clean state
    ufw --force reset > /dev/null 2>&1

    # Default policies
    ufw default deny incoming > /dev/null
    ufw default allow outgoing > /dev/null

    # Allow SSH (critical — don't lock yourself out)
    ufw allow 22/tcp comment "SSH" > /dev/null

    # Allow HTTP and HTTPS only
    ufw allow 80/tcp comment "HTTP -> HTTPS redirect" > /dev/null
    ufw allow 443/tcp comment "HTTPS (${PRODUCT_NAME})" > /dev/null

    # Enable firewall
    ufw --force enable > /dev/null

    log_ok "UFW enabled: SSH (22), HTTP (80), HTTPS (443) allowed; all else denied"
    log_info "Docker internal ports (5432, 6379, 6432, etc.) are NOT exposed to the network"
}

# ── Step 11: systemd Service ────────────────────────────────────────
step_create_systemd() {
    log_step "11" "Systemd Service & Backup Timer"

    if [[ "${DRY_RUN}" == "true" ]]; then
        log_info "[DRY RUN] Would create ${SLUG}.service, ${SLUG}-backup.service/.timer"
        return
    fi

    # Build compose profile args
    local profile_args=""
    if [[ "${ENABLE_MONITORING}" == "true" ]]; then
        profile_args="${profile_args} --profile monitoring"
    fi
    if [[ "${ENABLE_AI}" == "true" ]]; then
        profile_args="${profile_args} --profile ai"
    fi

    # ── Main service unit ────────────────────────────────────────────
    cat > "${SYSTEMD_DIR}/${SLUG}.service" <<UNIT
[Unit]
Description=${PRODUCT_NAME}
Documentation=${VENDOR_WEBSITE}
After=docker.service
Requires=docker.service

[Service]
Type=oneshot
RemainAfterExit=yes
WorkingDirectory=${INSTALL_DIR}
EnvironmentFile=${ENV_FILE}
ExecStart=/usr/bin/docker compose${profile_args} up -d --remove-orphans
ExecStop=/usr/bin/docker compose down
ExecReload=/usr/bin/docker compose${profile_args} up -d --remove-orphans
TimeoutStartSec=300
TimeoutStopSec=120

[Install]
WantedBy=multi-user.target
UNIT

    # ── Backup service ───────────────────────────────────────────────
    cat > "${SYSTEMD_DIR}/${SLUG}-backup.service" <<UNIT
[Unit]
Description=${PRODUCT_NAME} — Nightly Database Backup
After=docker.service ${SLUG}.service

[Service]
Type=oneshot
ExecStart=/bin/bash ${INSTALL_DIR}/deploy/installer/backup.sh
Environment=BACKUP_DIR=${BACKUP_DIR}
Environment=COMPOSE_PROJECT=${SLUG}
Environment=DB_CONTAINER=apple-pos-postgres
Environment=DB_USER=posadmin
Environment=DB_NAME=posdb_live
UNIT

    # ── Backup timer (02:00 every night) ─────────────────────────────
    cat > "${SYSTEMD_DIR}/${SLUG}-backup.timer" <<UNIT
[Unit]
Description=${PRODUCT_NAME} — Nightly Backup Schedule

[Timer]
OnCalendar=*-*-* 02:00:00
Persistent=true
RandomizedDelaySec=300

[Install]
WantedBy=timers.target
UNIT

    # Reload and enable
    systemctl daemon-reload
    systemctl enable "${SLUG}.service"
    systemctl enable "${SLUG}-backup.timer"
    systemctl start "${SLUG}-backup.timer"

    log_ok "Systemd units created and enabled:"
    log_info "  ${SLUG}.service        — start/stop the application stack"
    log_info "  ${SLUG}-backup.timer   — nightly database backup at 02:00"
}

# ── Step 12: Create Backup Script ────────────────────────────────────
step_create_backup_script() {
    # Write the backup script that the systemd timer calls
    local backup_script="${INSTALL_DIR}/deploy/installer/backup.sh"

    if [[ "${DRY_RUN}" == "true" ]]; then
        log_info "[DRY RUN] Would create backup script at ${backup_script}"
        return
    fi

    mkdir -p "$(dirname "${backup_script}")"

    cat > "${backup_script}" <<'BACKUP'
#!/usr/bin/env bash
set -Eeuo pipefail
# ═══════════════════════════════════════════════════════════════════════
# Apple Supermarket POS & ERP System — Database Backup Script
# Called by systemd timer: apple-pos-backup.timer
# ═══════════════════════════════════════════════════════════════════════

BACKUP_DIR="${BACKUP_DIR:-/var/lib/apple-pos/backups}"
DB_CONTAINER="${DB_CONTAINER:-apple-pos-postgres}"
DB_USER="${DB_USER:-posadmin}"
DB_NAME="${DB_NAME:-posdb_live}"
RETENTION_DAYS=30
DATE="$(date +%Y%m%d_%H%M%S)"
BACKUP_FILE="${BACKUP_DIR}/${DB_NAME}-${DATE}.sql.gz"
LOG_FILE="/var/log/apple-pos/backup.log"

log() { echo "$(date '+%Y-%m-%d %H:%M:%S') $1" | tee -a "${LOG_FILE}"; }

mkdir -p "${BACKUP_DIR}"

log "[INFO] Starting backup of ${DB_NAME}..."

# Dump via the running PostgreSQL container
if docker exec "${DB_CONTAINER}" pg_dump \
    -U "${DB_USER}" \
    -d "${DB_NAME}" \
    --format=custom \
    --compress=6 \
    2>>"${LOG_FILE}" > "${BACKUP_FILE%.gz}"; then

    gzip "${BACKUP_FILE%.gz}"
    FILESIZE=$(du -sh "${BACKUP_FILE}" | cut -f1)
    log "[OK] Backup successful: ${BACKUP_FILE} (${FILESIZE})"
else
    log "[ERROR] Backup FAILED for ${DB_NAME}"
    rm -f "${BACKUP_FILE}" "${BACKUP_FILE%.gz}" 2>/dev/null
    exit 1
fi

# Retention: remove backups older than N days
DELETED=$(find "${BACKUP_DIR}" -type f -name "*.sql.gz" -mtime "+${RETENTION_DAYS}" -delete -print | wc -l)
if [[ "${DELETED}" -gt 0 ]]; then
    log "[INFO] Cleaned up ${DELETED} backup(s) older than ${RETENTION_DAYS} days"
fi

log "[INFO] Backup complete"
BACKUP

    chmod 750 "${backup_script}"
    log_ok "Backup script created: ${backup_script}"
}

# ── Step 13: Build & Start Containers ────────────────────────────────
step_docker_compose_up() {
    log_step "13" "Build & Start Application Containers"

    if [[ "${DRY_RUN}" == "true" ]]; then
        log_info "[DRY RUN] Would run: docker compose up -d --build"
        return
    fi

    cd "${INSTALL_DIR}"

    # Build compose profile args
    local profile_args=()
    if [[ "${ENABLE_MONITORING}" == "true" ]]; then
        profile_args+=(--profile monitoring)
    fi
    if [[ "${ENABLE_AI}" == "true" ]]; then
        profile_args+=(--profile ai)
    fi

    log_info "Building Docker images (this may take 5-10 minutes on first run)..."
    docker compose -f "${COMPOSE_FILE}" "${profile_args[@]}" build 2>&1 | \
        tee -a "${INSTALL_LOG}"

    log_info "Starting containers..."
    docker compose -f "${COMPOSE_FILE}" "${profile_args[@]}" up -d --remove-orphans 2>&1 | \
        tee -a "${INSTALL_LOG}"

    # Wait for health checks
    log_info "Waiting for services to become healthy..."
    local max_wait=120
    local waited=0
    local all_healthy=false

    while [[ ${waited} -lt ${max_wait} ]]; do
        sleep 5
        waited=$((waited + 5))

        local unhealthy
        unhealthy=$(docker compose -f "${COMPOSE_FILE}" ps --format json 2>/dev/null | \
            jq -r 'select(.Health != "healthy" and .Health != "" and .State == "running") | .Name' 2>/dev/null | wc -l || echo "0")

        if [[ "${unhealthy}" -eq 0 ]]; then
            all_healthy=true
            break
        fi
        printf "\r  Waiting... %ds / %ds" "${waited}" "${max_wait}"
    done
    echo ""

    if [[ "${all_healthy}" == "true" ]]; then
        log_ok "All containers are healthy"
    else
        log_warn "Some containers may still be starting. Checking status..."
    fi

    docker compose -f "${COMPOSE_FILE}" ps 2>&1 | tee -a "${INSTALL_LOG}"
}

# ── Step 14: Post-Install Health Check ──────────────────────────────
step_health_check() {
    log_step "14" "Post-Installation Health Check"

    if [[ "${DRY_RUN}" == "true" ]]; then
        log_info "[DRY RUN] Would verify all services respond correctly"
        return
    fi

    local all_pass=true

    # Check backend health
    log_info "Checking backend health..."
    local backend_status
    backend_status=$(curl -sf --max-time 10 -o /dev/null -w "%{http_code}" \
        http://127.0.0.1:8000/api/health 2>/dev/null || echo "000")
    if [[ "${backend_status}" == "200" ]]; then
        log_ok "Backend API: healthy (HTTP ${backend_status})"
    else
        log_warn "Backend API: HTTP ${backend_status} (may still be starting)"
        all_pass=false
    fi

    # Check frontend
    log_info "Checking frontend..."
    local frontend_status
    frontend_status=$(curl -sf --max-time 10 -o /dev/null -w "%{http_code}" \
        http://127.0.0.1:8000/ 2>/dev/null || echo "000")
    if [[ "${frontend_status}" == "200" ]]; then
        log_ok "Frontend: accessible (HTTP ${frontend_status})"
    else
        log_warn "Frontend: HTTP ${frontend_status} (may still be starting)"
        all_pass=false
    fi

    # Check HTTPS via host Nginx
    log_info "Checking HTTPS access..."
    local https_status
    https_status=$(curl -skf --max-time 10 -o /dev/null -w "%{http_code}" \
        "https://${SERVER_FQDN}/" 2>/dev/null || echo "000")
    if [[ "${https_status}" == "200" ]]; then
        log_ok "HTTPS: accessible at https://${SERVER_FQDN}/"
    else
        log_warn "HTTPS: HTTP ${https_status} (check Nginx and TLS config)"
        all_pass=false
    fi

    # Check database
    log_info "Checking database..."
    if docker exec apple-pos-postgres pg_isready -U posadmin -d posdb_live &>/dev/null; then
        log_ok "PostgreSQL: accepting connections"
    else
        log_warn "PostgreSQL: not ready"
        all_pass=false
    fi

    # Check Redis
    log_info "Checking Redis..."
    if docker exec apple-pos-redis redis-cli ping 2>/dev/null | grep -q "PONG"; then
        log_ok "Redis: responding"
    else
        log_warn "Redis: not responding"
        all_pass=false
    fi

    echo ""
    if [[ "${all_pass}" == "true" ]]; then
        log_ok "All health checks passed!"
    else
        log_warn "Some services are still starting. Allow 1-2 minutes and check:"
        log_info "  docker compose -f ${COMPOSE_FILE} ps"
        log_info "  docker compose -f ${COMPOSE_FILE} logs --tail=50"
    fi
}

# ── Step 15: Print Summary ──────────────────────────────────────────
step_print_summary() {
    local border="═══════════════════════════════════════════════════════════════"

    echo ""
    printf "${BOLD}${GREEN}%s${NC}\n" "${border}"
    printf "${BOLD}${GREEN}  ✅ INSTALLATION COMPLETE${NC}\n"
    printf "${BOLD}${GREEN}%s${NC}\n" "${border}"
    echo ""

    printf "${BOLD}  Product:${NC}    %s v%s\n" "${PRODUCT_NAME}" "${PRODUCT_VERSION}"
    printf "${BOLD}  Company:${NC}    %s\n" "${COMPANY_NAME}"
    printf "${BOLD}  Access URL:${NC} %s\n" "https://${SERVER_FQDN}/"
    printf "${BOLD}  Timezone:${NC}   %s\n" "${SYSTEM_TIMEZONE}"
    echo ""

    printf "${BOLD}${CYAN}  ── Login Credentials ────────────────────────────────${NC}\n"
    printf "  Admin Email:      %s\n" "${ADMIN_EMAIL}"
    printf "  Admin Password:   %s\n" "(as entered during setup)"
    printf "  Admin PIN:        %s\n" "${GEN_ADMIN_PIN}"
    printf "  Cashier Password: %s\n" "${GEN_CASHIER_PASSWORD}"
    echo ""

    printf "${BOLD}${CYAN}  ── Generated Secrets (saved to ${ENV_FILE}) ────────${NC}\n"
    printf "  DB Password:      %s\n" "${GEN_POSADMIN_PASSWORD}"
    printf "  JWT Secret:       %s...\n" "${GEN_JWT_SECRET:0:20}"
    if [[ "${ENABLE_MONITORING}" == "true" ]]; then
        printf "  Grafana Password: %s\n" "${GEN_GRAFANA_PASSWORD}"
    fi
    echo ""

    printf "${BOLD}${YELLOW}  ⚠  SAVE THESE CREDENTIALS NOW — they will not be shown again.${NC}\n"
    printf "${BOLD}${YELLOW}  ⚠  The .env file (${ENV_FILE}) is root-readable only.${NC}\n"
    echo ""

    printf "${BOLD}${CYAN}  ── Service Management ───────────────────────────────${NC}\n"
    printf "  Start:   sudo systemctl start %s\n" "${SLUG}"
    printf "  Stop:    sudo systemctl stop %s\n" "${SLUG}"
    printf "  Status:  sudo systemctl status %s\n" "${SLUG}"
    printf "  Logs:    docker compose -f %s logs --tail=100 -f\n" "${COMPOSE_FILE}"
    echo ""

    if [[ "${ENABLE_MONITORING}" == "true" ]]; then
        printf "${BOLD}${CYAN}  ── Monitoring (SSH Tunnel Access) ───────────────────${NC}\n"
        printf "  Grafana: ssh -L 3000:localhost:3000 user@%s\n" "${SERVER_IP}"
        printf "           Then open http://localhost:3000 (admin / ${GEN_GRAFANA_PASSWORD})\n"
        printf "  Prometheus: ssh -L 9090:localhost:9090 user@%s\n" "${SERVER_IP}"
        echo ""
    fi

    printf "${BOLD}${CYAN}  ── Backups ──────────────────────────────────────────${NC}\n"
    printf "  Schedule:  Nightly at 02:00 (systemd timer)\n"
    printf "  Location:  %s\n" "${BACKUP_DIR}"
    printf "  Retention: 30 days\n"
    printf "  Manual:    sudo systemctl start %s-backup\n" "${SLUG}"
    echo ""

    printf "${BOLD}${CYAN}  ── Files & Paths ────────────────────────────────────${NC}\n"
    printf "  Install dir:  %s\n" "${INSTALL_DIR}"
    printf "  Data dir:     %s\n" "${DATA_DIR}"
    printf "  Backups:      %s\n" "${BACKUP_DIR}"
    printf "  Logs:         %s\n" "${LOG_DIR}"
    printf "  Env file:     %s\n" "${ENV_FILE}"
    printf "  Compose:      %s\n" "${COMPOSE_FILE}"
    printf "  Nginx:        %s\n" "${NGINX_SITE}"
    printf "  TLS certs:    %s\n" "${CERT_DIR}"
    echo ""

    printf "${BOLD}${CYAN}  ── Support ──────────────────────────────────────────${NC}\n"
    printf "  Email: %s\n" "${VENDOR_SUPPORT_EMAIL}"
    printf "  Phone: %s\n" "${VENDOR_SUPPORT_PHONE}"
    printf "  Hours: %s\n" "${VENDOR_SUPPORT_HOURS}"
    printf "  SLA:   %s\n" "${VENDOR_SLA}"
    echo ""

    printf "${GREEN}%s${NC}\n" "${border}"
    log_info "Full installation log: ${INSTALL_LOG}"
}

# ═══════════════════════════════════════════════════════════════════════
#  UNINSTALL (basic — see uninstall.sh for full version)
# ═══════════════════════════════════════════════════════════════════════
do_uninstall() {
    local uninstaller="${SCRIPT_DIR}/uninstall.sh"
    if [[ -f "${uninstaller}" ]]; then
        exec bash "${uninstaller}" "$@"
    fi

    banner
    log_warn "This will remove the ${PRODUCT_NAME} installation."
    log_warn "Data volumes will be PRESERVED unless you use uninstall.sh --purge."
    echo ""
    read -rp "Type '${SLUG}' to confirm removal: " confirm
    if [[ "${confirm}" != "${SLUG}" ]]; then
        log_info "Uninstall cancelled."
        exit 0
    fi

    rollback

    # Remove application directory (but not data/backups)
    if [[ -d "${INSTALL_DIR}" ]]; then
        rm -rf "${INSTALL_DIR}"
        log_ok "Removed ${INSTALL_DIR}"
    fi

    log_ok "Uninstall complete. Data preserved at ${DATA_DIR}"
    log_info "To fully purge all data: sudo bash uninstall.sh --purge"
}

# ═══════════════════════════════════════════════════════════════════════
#  MAIN
# ═══════════════════════════════════════════════════════════════════════
main() {
    parse_args "$@"

    # Ensure log directory exists early
    mkdir -p "${LOG_DIR}" 2>/dev/null || true
    touch "${INSTALL_LOG}" 2>/dev/null || true

    banner

    if [[ "${DO_UNINSTALL}" == "true" ]]; then
        do_uninstall
        exit 0
    fi

    if [[ "${DRY_RUN}" == "true" ]]; then
        log_warn "DRY RUN MODE — no changes will be made"
    fi

    INSTALL_STARTED=true

    # Phase 1: Pre-flight & Configuration
    run_preflight
    run_prompts

    # Phase 2: System Setup
    step_system_update
    step_install_docker
    step_create_dirs

    # Phase 3: Application Deployment
    step_deploy_application
    step_generate_env
    step_create_backup_script

    # Phase 4: Network & Security
    step_configure_tls
    step_configure_host_nginx
    step_configure_ufw

    # Phase 5: Services & Startup
    step_create_systemd
    step_docker_compose_up

    # Phase 6: Verification
    step_health_check
    step_print_summary
}

main "$@"
