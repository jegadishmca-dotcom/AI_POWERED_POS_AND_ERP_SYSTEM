#!/usr/bin/env bash
set -Eeuo pipefail
# ═══════════════════════════════════════════════════════════════════════
# brand.sh — Substitute vendor tokens in documentation templates
# ═══════════════════════════════════════════════════════════════════════
# Reads vendor.conf, replaces every {{TOKEN}} in .template.md files,
# and writes the final .md files. Fails loudly if any token remains.
#
# Usage:
#   bash brand.sh                # uses ./vendor.conf
#   bash brand.sh /path/to/vendor.conf
# ═══════════════════════════════════════════════════════════════════════

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
VENDOR_CONF="${1:-${SCRIPT_DIR}/vendor.conf}"

# ── Colours ──────────────────────────────────────────────────────────
RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'
CYAN='\033[0;36m'; NC='\033[0m'

log_info()  { printf "${CYAN}[INFO]${NC}  %s\n"  "$1"; }
log_ok()    { printf "${GREEN}[OK]${NC}    %s\n"  "$1"; }
log_warn()  { printf "${YELLOW}[WARN]${NC}  %s\n" "$1"; }
log_error() { printf "${RED}[ERROR]${NC} %s\n"    "$1" >&2; }

# ── Load vendor.conf ─────────────────────────────────────────────────
if [[ ! -f "${VENDOR_CONF}" ]]; then
    log_error "vendor.conf not found at: ${VENDOR_CONF}"
    log_error "Copy vendor.conf.example and fill in your vendor identity."
    exit 1
fi

# shellcheck source=/dev/null
source "${VENDOR_CONF}"

log_info "Loaded vendor config from: ${VENDOR_CONF}"
log_info "Vendor: ${VENDOR_COMPANY}"
log_info "Product: ${PRODUCT_NAME} v${PRODUCT_VERSION}"

# ── Define token → value mapping ────────────────────────────────────
declare -A TOKENS=(
    ["{{VENDOR_COMPANY}}"]="${VENDOR_COMPANY}"
    ["{{VENDOR_SUPPORT_EMAIL}}"]="${VENDOR_SUPPORT_EMAIL}"
    ["{{VENDOR_SUPPORT_PHONE}}"]="${VENDOR_SUPPORT_PHONE}"
    ["{{VENDOR_WEBSITE}}"]="${VENDOR_WEBSITE}"
    ["{{VENDOR_ADDRESS}}"]="${VENDOR_ADDRESS}"
    ["{{VENDOR_COPYRIGHT_YEAR}}"]="${VENDOR_COPYRIGHT_YEAR}"
    ["{{VENDOR_SUPPORT_HOURS}}"]="${VENDOR_SUPPORT_HOURS}"
    ["{{VENDOR_SLA}}"]="${VENDOR_SLA}"
    ["{{VENDOR_CLASSIFICATION}}"]="${VENDOR_CLASSIFICATION}"
    ["{{PRODUCT_NAME}}"]="${PRODUCT_NAME}"
    ["{{PRODUCT_VERSION}}"]="${PRODUCT_VERSION}"
    ["{{PRODUCT_SLUG}}"]="${PRODUCT_SLUG}"
    ["{{REPO_URL}}"]="${REPO_URL}"
    ["{{RELEASE_TAG}}"]="${RELEASE_TAG}"
)

# ── Process each template ───────────────────────────────────────────
TEMPLATES=("INSTALLATION_GUIDE.template.md" "README.template.md")
FAIL=false

for template_name in "${TEMPLATES[@]}"; do
    template_path="${SCRIPT_DIR}/${template_name}"
    output_name="${template_name%.template.md}.md"
    output_path="${SCRIPT_DIR}/${output_name}"

    if [[ ! -f "${template_path}" ]]; then
        log_warn "Template not found, skipping: ${template_name}"
        continue
    fi

    log_info "Processing: ${template_name} → ${output_name}"

    # Read template content
    content="$(cat "${template_path}")"

    # Substitute each token
    for token in "${!TOKENS[@]}"; do
        value="${TOKENS[${token}]}"
        # Escape special sed characters in the value
        escaped_value="$(printf '%s' "${value}" | sed -e 's/[&/\]/\\&/g')"
        escaped_token="$(printf '%s' "${token}" | sed -e 's/[{}\[\]]/\\&/g')"
        content="$(printf '%s' "${content}" | sed "s/${escaped_token}/${escaped_value}/g")"
    done

    # Write output
    printf '%s\n' "${content}" > "${output_path}"

    # Verify: check for any remaining {{...}} tokens
    remaining="$(grep -oP '\{\{[A-Z_]+\}\}' "${output_path}" 2>/dev/null | sort -u || true)"
    if [[ -n "${remaining}" ]]; then
        log_error "UNSUBSTITUTED TOKENS in ${output_name}:"
        printf '%s\n' "${remaining}" | while read -r tok; do
            log_error "  ${tok}"
        done
        FAIL=true
    else
        log_ok "${output_name} — all tokens substituted successfully"
    fi
done

# ── Final verdict ───────────────────────────────────────────────────
echo ""
if [[ "${FAIL}" == "true" ]]; then
    log_error "═══════════════════════════════════════════════════════════"
    log_error "BRANDING FAILED: unsubstituted tokens remain."
    log_error "Add missing variables to vendor.conf and re-run brand.sh."
    log_error "DO NOT ship these documents to a customer."
    log_error "═══════════════════════════════════════════════════════════"
    exit 1
else
    log_ok "═══════════════════════════════════════════════════════════"
    log_ok "Branding complete. Documents are ready for distribution."
    log_ok "═══════════════════════════════════════════════════════════"
fi
