-- =====================================================================
-- Enterprise Supermarket POS & ERP System - Core Database Schema (Step 2)
-- PostgreSQL 16
-- =====================================================================

-- Enable necessary extensions
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- ==========================================
-- 1. AUTHENTICATION & AUTHORIZATION
-- ==========================================

-- ROLES
CREATE TABLE roles (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    store_id UUID, -- Multi-store readiness
    name VARCHAR(50) NOT NULL,
    description TEXT,
    -- Audit & Soft Delete
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    created_by UUID,
    updated_at TIMESTAMP WITH TIME ZONE,
    updated_by UUID,
    is_deleted BOOLEAN DEFAULT FALSE NOT NULL,
    deleted_at TIMESTAMP WITH TIME ZONE,
    UNIQUE (store_id, name)
);
-- RLS Comment: ALTER TABLE roles ENABLE ROW LEVEL SECURITY;

-- PERMISSIONS
CREATE TABLE permissions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(100) NOT NULL UNIQUE,
    module VARCHAR(50) NOT NULL,
    -- Audit & Soft Delete
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    created_by UUID,
    updated_at TIMESTAMP WITH TIME ZONE,
    updated_by UUID,
    is_deleted BOOLEAN DEFAULT FALSE NOT NULL,
    deleted_at TIMESTAMP WITH TIME ZONE
);
-- Permissions are global metadata, usually no RLS or store_id needed, but added for consistency if required.

-- ROLE_PERMISSIONS
CREATE TABLE role_permissions (
    role_id UUID REFERENCES roles(id) ON DELETE CASCADE,
    permission_id UUID REFERENCES permissions(id) ON DELETE CASCADE,
    store_id UUID, -- Multi-store readiness
    PRIMARY KEY (role_id, permission_id)
);
-- RLS Comment: ALTER TABLE role_permissions ENABLE ROW LEVEL SECURITY;

-- USERS
CREATE TABLE users (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    store_id UUID, -- Multi-store readiness
    username VARCHAR(50) NOT NULL UNIQUE,
    password_hash VARCHAR(255) NOT NULL,
    full_name VARCHAR(100) NOT NULL,
    pin_hash VARCHAR(255), -- For fast POS login
    role_id UUID NOT NULL REFERENCES roles(id),
    is_active BOOLEAN DEFAULT TRUE NOT NULL,
    -- Audit & Soft Delete
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    created_by UUID,
    updated_at TIMESTAMP WITH TIME ZONE,
    updated_by UUID,
    is_deleted BOOLEAN DEFAULT FALSE NOT NULL,
    deleted_at TIMESTAMP WITH TIME ZONE
);
-- RLS Comment: ALTER TABLE users ENABLE ROW LEVEL SECURITY;
CREATE INDEX idx_users_username ON users(username) WHERE is_deleted = FALSE;


-- ==========================================
-- 2. TERMINALS / DEVICES
-- ==========================================

CREATE TABLE terminals (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    store_id UUID, -- Multi-store readiness
    terminal_code VARCHAR(20) NOT NULL,
    name VARCHAR(100) NOT NULL,
    mac_address VARCHAR(50),
    is_active BOOLEAN DEFAULT TRUE NOT NULL,
    -- Audit & Soft Delete
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    created_by UUID,
    updated_at TIMESTAMP WITH TIME ZONE,
    updated_by UUID,
    is_deleted BOOLEAN DEFAULT FALSE NOT NULL,
    deleted_at TIMESTAMP WITH TIME ZONE,
    UNIQUE (store_id, terminal_code)
);
-- RLS Comment: ALTER TABLE terminals ENABLE ROW LEVEL SECURITY;
CREATE INDEX idx_terminals_code ON terminals(terminal_code) WHERE is_deleted = FALSE;


-- ==========================================
-- 3. PRODUCT CATALOG
-- ==========================================

-- CATEGORIES
CREATE TABLE categories (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    store_id UUID, -- Multi-store readiness
    name VARCHAR(100) NOT NULL,
    parent_category_id UUID REFERENCES categories(id),
    -- Audit & Soft Delete
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    created_by UUID,
    updated_at TIMESTAMP WITH TIME ZONE,
    updated_by UUID,
    is_deleted BOOLEAN DEFAULT FALSE NOT NULL,
    deleted_at TIMESTAMP WITH TIME ZONE
);
-- RLS Comment: ALTER TABLE categories ENABLE ROW LEVEL SECURITY;

-- BRANDS
CREATE TABLE brands (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    store_id UUID, -- Multi-store readiness
    name VARCHAR(100) NOT NULL,
    -- Audit & Soft Delete
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    created_by UUID,
    updated_at TIMESTAMP WITH TIME ZONE,
    updated_by UUID,
    is_deleted BOOLEAN DEFAULT FALSE NOT NULL,
    deleted_at TIMESTAMP WITH TIME ZONE
);
-- RLS Comment: ALTER TABLE brands ENABLE ROW LEVEL SECURITY;

-- TAX SLABS
CREATE TABLE tax_slabs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    store_id UUID, -- Multi-store readiness
    name VARCHAR(50) NOT NULL,
    cgst_rate DECIMAL(18,4) DEFAULT 0 NOT NULL,
    sgst_rate DECIMAL(18,4) DEFAULT 0 NOT NULL,
    igst_rate DECIMAL(18,4) DEFAULT 0 NOT NULL,
    cess_rate DECIMAL(18,4) DEFAULT 0 NOT NULL,
    -- Audit & Soft Delete
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    created_by UUID,
    updated_at TIMESTAMP WITH TIME ZONE,
    updated_by UUID,
    is_deleted BOOLEAN DEFAULT FALSE NOT NULL,
    deleted_at TIMESTAMP WITH TIME ZONE
);
-- RLS Comment: ALTER TABLE tax_slabs ENABLE ROW LEVEL SECURITY;

-- PRODUCTS
CREATE TABLE products (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    store_id UUID, -- Multi-store readiness
    product_code VARCHAR(50) NOT NULL,
    name VARCHAR(200) NOT NULL,
    tamil_name VARCHAR(200), -- Tamil Support
    description TEXT,
    category_id UUID REFERENCES categories(id),
    brand_id UUID REFERENCES brands(id),
    tax_slab_id UUID NOT NULL REFERENCES tax_slabs(id),
    hsn_code VARCHAR(20),
    is_weighable BOOLEAN DEFAULT FALSE NOT NULL,
    mrp DECIMAL(18,4) NOT NULL,
    selling_price DECIMAL(18,4) NOT NULL,
    purchase_price DECIMAL(18,4) NOT NULL,
    current_stock DECIMAL(18,4) DEFAULT 0 NOT NULL,
    is_active BOOLEAN DEFAULT TRUE NOT NULL,
    
    -- Full-text search vector
    search_vector TSVECTOR,

    -- Audit & Soft Delete
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    created_by UUID,
    updated_at TIMESTAMP WITH TIME ZONE,
    updated_by UUID,
    is_deleted BOOLEAN DEFAULT FALSE NOT NULL,
    deleted_at TIMESTAMP WITH TIME ZONE,
    UNIQUE (store_id, product_code)
);

-- Full-text search trigger
CREATE OR REPLACE FUNCTION products_search_vector_trigger() RETURNS trigger AS $$
BEGIN
  NEW.search_vector :=
    setweight(to_tsvector('english', coalesce(NEW.name, '')), 'A') ||
    setweight(to_tsvector('english', coalesce(NEW.product_code, '')), 'A') ||
    setweight(to_tsvector('english', coalesce(NEW.tamil_name, '')), 'B') ||
    setweight(to_tsvector('english', coalesce(NEW.hsn_code, '')), 'C');
  RETURN NEW;
END
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_products_search_vector
BEFORE INSERT OR UPDATE ON products
FOR EACH ROW EXECUTE FUNCTION products_search_vector_trigger();

-- RLS Comment: ALTER TABLE products ENABLE ROW LEVEL SECURITY;
CREATE INDEX idx_products_code ON products(product_code) WHERE is_deleted = FALSE;
CREATE INDEX idx_products_category ON products(category_id) WHERE is_deleted = FALSE;
CREATE INDEX idx_products_hsn ON products(hsn_code) WHERE is_deleted = FALSE;
CREATE INDEX idx_products_search ON products USING GIN(search_vector);

-- BARCODES
CREATE TABLE barcodes (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    store_id UUID, -- Multi-store readiness
    product_id UUID NOT NULL REFERENCES products(id) ON DELETE CASCADE,
    barcode VARCHAR(100) NOT NULL,
    is_primary BOOLEAN DEFAULT FALSE NOT NULL,
    -- Audit & Soft Delete
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    created_by UUID,
    updated_at TIMESTAMP WITH TIME ZONE,
    updated_by UUID,
    is_deleted BOOLEAN DEFAULT FALSE NOT NULL,
    deleted_at TIMESTAMP WITH TIME ZONE,
    UNIQUE(store_id, barcode)
);
-- RLS Comment: ALTER TABLE barcodes ENABLE ROW LEVEL SECURITY;
CREATE INDEX idx_barcodes_barcode ON barcodes(barcode) WHERE is_deleted = FALSE;
CREATE INDEX idx_barcodes_product ON barcodes(product_id) WHERE is_deleted = FALSE;


-- ==========================================
-- 4. POS BILLING (PARTITIONING READINESS)
-- ==========================================

-- INVOICES (Partitioned by business_date)
CREATE TABLE invoices (
    id UUID NOT NULL DEFAULT gen_random_uuid(),
    store_id UUID, -- Multi-store readiness
    business_date DATE NOT NULL,
    invoice_number VARCHAR(50) NOT NULL,
    
    terminal_id UUID NOT NULL, -- Logical ref to terminals
    terminal_sequence INT NOT NULL, -- Daily sequence per terminal
    
    cashier_id UUID NOT NULL, -- Logical ref to users
    
    -- Totals
    sub_total DECIMAL(18,4) NOT NULL,
    discount_amount DECIMAL(18,4) DEFAULT 0 NOT NULL,
    tax_amount DECIMAL(18,4) NOT NULL,
    total_amount DECIMAL(18,4) NOT NULL,
    round_off DECIMAL(18,4) DEFAULT 0 NOT NULL,
    net_payable DECIMAL(18,4) NOT NULL,
    
    -- Indian Compliance (E-Invoicing hooks)
    irn VARCHAR(64),
    ack_no VARCHAR(20),
    ack_date TIMESTAMP WITH TIME ZONE,
    qr_code TEXT,
    
    status VARCHAR(20) DEFAULT 'COMPLETED' NOT NULL, -- COMPLETED, CANCELLED, HOLD
    payment_mode VARCHAR(20) NOT NULL, -- CASH, CARD, UPI
    
    -- Audit & Soft Delete
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    created_by UUID,
    updated_at TIMESTAMP WITH TIME ZONE,
    updated_by UUID,
    is_deleted BOOLEAN DEFAULT FALSE NOT NULL,
    deleted_at TIMESTAMP WITH TIME ZONE,

    PRIMARY KEY (id, business_date),
    UNIQUE (terminal_id, terminal_sequence, business_date)
) PARTITION BY RANGE (business_date);

-- RLS Comment: ALTER TABLE invoices ENABLE ROW LEVEL SECURITY;
CREATE INDEX idx_invoices_number ON invoices(invoice_number);
CREATE INDEX idx_invoices_date ON invoices(business_date);
CREATE INDEX idx_invoices_terminal ON invoices(terminal_id);

-- Example Partition for current month
CREATE TABLE invoices_y2026m05 PARTITION OF invoices
    FOR VALUES FROM ('2026-05-01') TO ('2026-06-01');

-- INVOICE ITEMS (Partitioned by business_date)
CREATE TABLE invoice_items (
    id UUID NOT NULL DEFAULT gen_random_uuid(),
    store_id UUID, -- Multi-store readiness
    invoice_id UUID NOT NULL,
    business_date DATE NOT NULL,
    product_id UUID NOT NULL, 
    barcode VARCHAR(100),
    product_name VARCHAR(200) NOT NULL,
    
    quantity DECIMAL(18,4) NOT NULL,
    unit_price DECIMAL(18,4) NOT NULL,
    discount_amount DECIMAL(18,4) DEFAULT 0 NOT NULL,
    
    -- Tax Snapshot (Ensuring historical accuracy)
    cgst_rate DECIMAL(18,4) DEFAULT 0 NOT NULL,
    cgst_amount DECIMAL(18,4) DEFAULT 0 NOT NULL,
    sgst_rate DECIMAL(18,4) DEFAULT 0 NOT NULL,
    sgst_amount DECIMAL(18,4) DEFAULT 0 NOT NULL,
    cess_rate DECIMAL(18,4) DEFAULT 0 NOT NULL,
    cess_amount DECIMAL(18,4) DEFAULT 0 NOT NULL,
    
    total_amount DECIMAL(18,4) NOT NULL,
    
    -- Audit & Soft Delete
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    created_by UUID,
    updated_at TIMESTAMP WITH TIME ZONE,
    updated_by UUID,
    is_deleted BOOLEAN DEFAULT FALSE NOT NULL,
    deleted_at TIMESTAMP WITH TIME ZONE,

    PRIMARY KEY (id, business_date),
    FOREIGN KEY (invoice_id, business_date) REFERENCES invoices(id, business_date) ON DELETE CASCADE
) PARTITION BY RANGE (business_date);

-- RLS Comment: ALTER TABLE invoice_items ENABLE ROW LEVEL SECURITY;
CREATE INDEX idx_invoice_items_invoice ON invoice_items(invoice_id);
CREATE INDEX idx_invoice_items_product ON invoice_items(product_id);

-- Example Partition for current month
CREATE TABLE invoice_items_y2026m05 PARTITION OF invoice_items
    FOR VALUES FROM ('2026-05-01') TO ('2026-06-01');
