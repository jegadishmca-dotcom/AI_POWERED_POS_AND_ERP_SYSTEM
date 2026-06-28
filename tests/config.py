# =============================================================================
# tests/config.py - Shared configuration for all test layers
# =============================================================================
import os
os.environ.setdefault('PYTHONIOENCODING', 'utf-8')

# ── Database ──────────────────────────────────────────────────────────────────
DB_CONFIG = {
    "host": "10.26.198.140",
    "port": 5432,
    "database": "posdb",
    "user": "posadmin",
    "password": "pospassword",
}

# ── Backend API ───────────────────────────────────────────────────────────────
API_BASE_URL = "http://localhost:5000"

# ── Test Credentials ──────────────────────────────────────────────────────────
ADMIN_USER = {
    "username": "admin@supermarket.local",
    "password": "Admin@123!",
    "terminalCode": "LOCAL POS 01",
}

# ── Backend Source Path (for schema scanner) ──────────────────────────────────
import os
BACKEND_ENTITIES_PATH = os.path.join(
    os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
    "src", "Backend", "PosErp.Domain", "Entities"
)
DBCONTEXT_PATH = os.path.join(
    os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
    "src", "Backend", "PosErp.Infrastructure", "Persistence", "ApplicationDbContext.cs"
)

# ── Console Colors ────────────────────────────────────────────────────────────
class C:
    PASS   = "\033[92m"   # Green
    FAIL   = "\033[91m"   # Red
    WARN   = "\033[93m"   # Yellow
    INFO   = "\033[94m"   # Blue
    BOLD   = "\033[1m"
    RESET  = "\033[0m"
    HEADER = "\033[95m"   # Purple

def ok(msg):    print(f"  {C.PASS}[PASS]{C.RESET} {msg}")
def fail(msg):  print(f"  {C.FAIL}[FAIL]{C.RESET} {msg}")
def warn(msg):  print(f"  {C.WARN}[WARN]{C.RESET} {msg}")
def info(msg):  print(f"  {C.INFO}[INFO]{C.RESET} {msg}")
def header(msg):print(f"\n{C.BOLD}{C.HEADER}{'='*70}{C.RESET}\n{C.BOLD}{msg}{C.RESET}\n{'='*70}")
def section(msg):print(f"\n{C.BOLD}{'-'*50}\n{msg}\n{'-'*50}{C.RESET}")
