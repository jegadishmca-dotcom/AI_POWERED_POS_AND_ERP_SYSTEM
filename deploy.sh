#!/bin/bash
# ╔═══════════════════════════════════════════════════════════════╗
# ║          Apple Supermarket POS - Deploy Script                ║
# ║  Usage: sudo bash deploy.sh [--full | --backend | --frontend] ║
# ╚═══════════════════════════════════════════════════════════════╝

set -e

# --- Colors ---
RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'; CYAN='\033[0;36m'; NC='\033[0m'

log_info()  { echo -e "${CYAN}[INFO]${NC}  $1"; }
log_ok()    { echo -e "${GREEN}[OK]${NC}    $1"; }
log_warn()  { echo -e "${YELLOW}[WARN]${NC}  $1"; }
log_error() { echo -e "${RED}[ERROR]${NC} $1"; }

MODE="${1:---full}"
COMPOSE_FILE="docker-compose.yml"

echo ""
echo -e "${CYAN}============================================${NC}"
echo -e "${CYAN}   POS ERP - Deployment Script v1.0        ${NC}"
echo -e "${CYAN}============================================${NC}"
NO_PULL=false
for arg in "$@"; do
  [[ "$arg" == "--no-pull" ]] && NO_PULL=true
  [[ "$arg" == "--backend" ]] && MODE="--backend"
  [[ "$arg" == "--frontend" ]] && MODE="--frontend"
done

# --- Step 1: Git Pull (optional) ---
if [ "$NO_PULL" = true ]; then
  log_warn "Skipping git pull (--no-pull flag set). Using existing local code."
else
  log_info "Pulling latest code from GitHub..."
  if git pull origin main; then
    log_ok "Code is up to date."
  else
    log_warn "Git pull failed (network issue?). Continuing with existing local code..."
    log_warn "Run 'sudo bash deploy.sh --no-pull' to skip git pull next time."
  fi
fi
echo ""

# --- Step 2: Check Docker daemon ---
log_info "Checking Docker daemon..."
if ! docker info > /dev/null 2>&1; then
  log_error "Docker is not running! Start Docker and try again."
  exit 1
fi
log_ok "Docker is running."
echo ""

# --- Step 3: Build ---
case "$MODE" in
  --backend)
    log_info "Building backend only..."
    TARGET="pos_backend"
    ;;
  --frontend)
    log_info "Building frontend only..."
    TARGET="pos_frontend"
    ;;
  --full | *)
    log_info "Building all services (full deploy)..."
    TARGET="pos_backend pos_frontend"
    ;;
esac

log_info "Running: docker compose up -d --build $TARGET"
echo ""

if docker compose -f $COMPOSE_FILE up -d --build $TARGET; then
  log_ok "Build and deployment successful!"
else
  log_error "Build FAILED. Checking which service(s) are down..."
  echo ""
  docker compose ps
  echo ""
  log_warn "Attempting to show last 40 log lines from pos_backend for diagnostics:"
  docker compose logs --tail=40 pos_backend 2>/dev/null || true
  log_warn "Attempting to show last 40 log lines from pos_frontend for diagnostics:"
  docker compose logs --tail=40 pos_frontend 2>/dev/null || true
  echo ""
  log_error "Deploy failed. Fix the issues above and re-run: sudo bash deploy.sh"
  exit 1
fi

echo ""
# --- Step 4: Health Check ---
log_info "Waiting 10 seconds for services to initialize..."
sleep 10

log_info "Running health check..."

BACKEND_OK=false
FRONTEND_OK=false

# Check backend
HTTP_STATUS=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:5000/health 2>/dev/null || echo "000")
if [ "$HTTP_STATUS" = "200" ]; then
  BACKEND_OK=true
  log_ok "Backend is healthy (HTTP $HTTP_STATUS)"
else
  log_warn "Backend health check returned HTTP $HTTP_STATUS (may still be starting up)"
fi

# Check frontend
HTTP_STATUS=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:3000 2>/dev/null || echo "000")
if [ "$HTTP_STATUS" = "200" ]; then
  FRONTEND_OK=true
  log_ok "Frontend is healthy (HTTP $HTTP_STATUS)"
else
  log_warn "Frontend health check returned HTTP $HTTP_STATUS (may still be starting up)"
fi

echo ""
log_info "Container status:"
docker compose ps
echo ""

echo -e "${GREEN}============================================${NC}"
echo -e "${GREEN}   Deployment Complete!                     ${NC}"
echo -e "${GREEN}============================================${NC}"
echo ""
echo -e "  ${CYAN}App URL:${NC}     http://$(hostname -I | awk '{print $1}'):8000"
echo -e "  ${CYAN}Backend:${NC}     http://$(hostname -I | awk '{print $1}'):5000"
echo -e "  ${CYAN}Frontend:${NC}    http://$(hostname -I | awk '{print $1}'):3000"
echo ""
