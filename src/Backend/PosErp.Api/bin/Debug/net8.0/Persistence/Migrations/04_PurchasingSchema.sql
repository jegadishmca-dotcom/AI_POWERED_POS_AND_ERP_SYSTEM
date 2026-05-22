-- ==============================================================================
-- PHASE 2: PURCHASING (PO -> GRN -> BILL) SCHEMA
-- ==============================================================================

-- To resolve conflict with previous foundational GRN tables, we drop them if they exist
DROP TABLE IF EXISTS goods_receipt_note_items CASCADE;
DROP TABLE IF EXISTS goods_receipt_notes CASCADE;

CREATE TABLE purchase_order_headers (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    store_id UUID,
    supplier_id UUID NOT NULL,
    po_number VARCHAR(100) UNIQUE NOT NULL,
    po_date DATE NOT NULL,
    expected_delivery_date DATE NOT NULL,
    total_amount DECIMAL(18,4) NOT NULL,
    status VARCHAR(50) DEFAULT 'DRAFT',
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    created_by UUID
);

CREATE TABLE purchase_order_items (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    purchase_order_header_id UUID NOT NULL REFERENCES purchase_order_headers(id) ON DELETE CASCADE,
    product_id UUID NOT NULL REFERENCES products(id),
    ordered_quantity DECIMAL(18,4) NOT NULL,
    received_quantity DECIMAL(18,4) NOT NULL DEFAULT 0,
    unit_cost DECIMAL(18,4) NOT NULL,
    total_cost DECIMAL(18,4) NOT NULL
);

CREATE TABLE grn_headers (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    store_id UUID,
    purchase_order_header_id UUID REFERENCES purchase_order_headers(id),
    supplier_id UUID NOT NULL,
    grn_number VARCHAR(100) UNIQUE NOT NULL,
    supplier_invoice_number VARCHAR(100),
    received_date DATE NOT NULL,
    total_amount DECIMAL(18,4) NOT NULL,
    status VARCHAR(50) DEFAULT 'DRAFT',
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    created_by UUID
);

CREATE TABLE grn_items (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    grn_header_id UUID NOT NULL REFERENCES grn_headers(id) ON DELETE CASCADE,
    purchase_order_item_id UUID NOT NULL REFERENCES purchase_order_items(id),
    product_id UUID NOT NULL REFERENCES products(id),
    batch_id UUID REFERENCES product_batches(id),
    batch_number VARCHAR(100),
    expiry_date DATE,
    mfg_date DATE,
    received_quantity DECIMAL(18,4) NOT NULL,
    accepted_quantity DECIMAL(18,4) NOT NULL,
    rejected_quantity DECIMAL(18,4) NOT NULL,
    unit_cost DECIMAL(18,4) NOT NULL,
    total_cost DECIMAL(18,4) NOT NULL
);

CREATE TABLE purchase_bill_headers (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    store_id UUID,
    supplier_id UUID NOT NULL,
    grn_header_id UUID REFERENCES grn_headers(id),
    bill_number VARCHAR(100) UNIQUE NOT NULL,
    bill_date DATE NOT NULL,
    sub_total DECIMAL(18,4) NOT NULL,
    tax_amount DECIMAL(18,4) NOT NULL,
    total_amount DECIMAL(18,4) NOT NULL,
    status VARCHAR(50) DEFAULT 'PENDING_PAYMENT',
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    created_by UUID
);

CREATE TABLE purchase_bill_items (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    purchase_bill_header_id UUID NOT NULL REFERENCES purchase_bill_headers(id) ON DELETE CASCADE,
    product_id UUID NOT NULL REFERENCES products(id),
    quantity DECIMAL(18,4) NOT NULL,
    unit_cost DECIMAL(18,4) NOT NULL,
    tax_amount DECIMAL(18,4) NOT NULL,
    total_amount DECIMAL(18,4) NOT NULL
);
