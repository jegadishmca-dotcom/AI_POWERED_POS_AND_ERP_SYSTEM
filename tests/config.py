# =============================================================================
# tests/config.py - Shared configuration for all test layers
# =============================================================================
import os, uuid
os.environ.setdefault('PYTHONIOENCODING', 'utf-8')

# ── Test Suite Safety Settings ─────────────────────────────────────────────────
# All automated tests run against posdb_uat, never posdb_live.
# The TEST_RUN_ID is a short UUID stamped into every invoice number created by
# the test runner so that any record can be unambiguously traced back to a
# specific test execution (useful for audits and post-run cleanup queries).
TEST_MODE    = True
TEST_RUN_ID  = uuid.uuid4().hex[:8].upper()    # e.g. "A3F2C9B1" — changes each run
TEST_PREFIX  = f"TEST-{TEST_RUN_ID}"           # e.g. "TEST-A3F2C9B1-WF1-..." prefix

# ── Database ──────────────────────────────────────────────────────────────────
# Direct DB connection used by Layer 1 (schema scanner) and Layer 4 (accounting).
# Points at posdb_uat so schema/accounting checks never touch live data.
DB_CONFIG = {
    "host":     os.environ.get("TEST_DB_HOST",     "192.168.1.5"),
    "port":     int(os.environ.get("TEST_DB_PORT", "5432")),
    "database": os.environ.get("TEST_DB_NAME",     "posdb_uat"),
    "user":     os.environ.get("TEST_DB_USER",     "posadmin"),
    "password": os.environ.get("TEST_DB_PASS",     "pospassword"),
}

# ── Backend API ───────────────────────────────────────────────────────────────
# The test runner calls the live API endpoint (always).  The API itself decides
# which database to write to based on the server's active mode (LIVE/UAT).
# Before running tests, ensure the server is toggled to UAT mode via the admin UI.
API_BASE_URL = os.environ.get("TEST_API_URL", "http://192.168.1.5:8000")

# ── Test Credentials ──────────────────────────────────────────────────────────
ADMIN_USER = {
    "username": "admin@supermarket.local",
    "password": "Admin@123!",
    "terminalCode": "LOCAL POS 01",
}

# ── Backend Source Path (for schema scanner) ──────────────────────────────────
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

