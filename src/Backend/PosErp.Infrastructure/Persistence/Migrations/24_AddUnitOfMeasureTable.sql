-- =====================================================================
-- Add Unit Of Measure Table and Seed Default Categories & UOMs
-- =====================================================================

-- Create unit_of_measures table
CREATE TABLE IF NOT EXISTS unit_of_measures (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    store_id UUID,
    name VARCHAR(50) NOT NULL,
    symbol VARCHAR(20) NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    is_deleted BOOLEAN DEFAULT FALSE NOT NULL
);

-- Seed standard UOMs
INSERT INTO unit_of_measures (id, name, symbol, created_at, is_deleted) VALUES
('a0000000-0000-0000-0000-000000000001', 'Pieces', 'Pcs', NOW(), FALSE),
('a0000000-0000-0000-0000-000000000002', 'Kilograms', 'Kgs', NOW(), FALSE),
('a0000000-0000-0000-0000-000000000003', 'Grams', 'Gms', NOW(), FALSE),
('a0000000-0000-0000-0000-000000000004', 'Litres', 'Ltrs', NOW(), FALSE),
('a0000000-0000-0000-0000-000000000005', 'Millilitres', 'Mls', NOW(), FALSE),
('a0000000-0000-0000-0000-000000000006', 'Packets', 'Pack', NOW(), FALSE),
('a0000000-0000-0000-0000-000000000007', 'Boxes', 'Box', NOW(), FALSE)
ON CONFLICT (id) DO NOTHING;

-- Seed standard default Category Hierarchy
INSERT INTO categories (id, name, parent_category_id, created_at, is_deleted) VALUES
('c0000000-0000-0000-0000-000000000001', 'Grocery', NULL, NOW(), FALSE),
('c0000000-0000-0000-0000-000000000002', 'Fresh Produce', NULL, NOW(), FALSE),
('c0000000-0000-0000-0000-000000000003', 'Dairy & Beverages', NULL, NOW(), FALSE),
('c0000000-0000-0000-0000-000000000004', 'Household & Personal Care', NULL, NOW(), FALSE)
ON CONFLICT (id) DO NOTHING;

INSERT INTO categories (id, name, parent_category_id, created_at, is_deleted) VALUES
('c0000000-0000-0000-0000-000000000011', 'Spices & Masalas', 'c0000000-0000-0000-0000-000000000001', NOW(), FALSE),
('c0000000-0000-0000-0000-000000000012', 'Flours & Grains', 'c0000000-0000-0000-0000-000000000001', NOW(), FALSE),
('c0000000-0000-0000-0000-000000000013', 'Rice & Rice Products', 'c0000000-0000-0000-0000-000000000001', NOW(), FALSE),
('c0000000-0000-0000-0000-000000000014', 'Oil & Ghee', 'c0000000-0000-0000-0000-000000000001', NOW(), FALSE),

('c0000000-0000-0000-0000-000000000021', 'Vegetables', 'c0000000-0000-0000-0000-000000000002', NOW(), FALSE),
('c0000000-0000-0000-0000-000000000022', 'Fruits', 'c0000000-0000-0000-0000-000000000002', NOW(), FALSE),

('c0000000-0000-0000-0000-000000000031', 'Milk & Curd', 'c0000000-0000-0000-0000-000000000003', NOW(), FALSE),
('c0000000-0000-0000-0000-000000000032', 'Soft Drinks & Juices', 'c0000000-0000-0000-0000-000000000003', NOW(), FALSE),
('c0000000-0000-0000-0000-000000000033', 'Tea & Coffee', 'c0000000-0000-0000-0000-000000000003', NOW(), FALSE),

('c0000000-0000-0000-0000-000000000041', 'Soaps & Detergents', 'c0000000-0000-0000-0000-000000000004', NOW(), FALSE),
('c0000000-0000-0000-0000-000000000042', 'Oral Care', 'c0000000-0000-0000-0000-000000000004', NOW(), FALSE),
('c0000000-0000-0000-0000-000000000043', 'Cleaning Supplies', 'c0000000-0000-0000-0000-000000000004', NOW(), FALSE)
ON CONFLICT (id) DO NOTHING;

-- Add unit_of_measure_id to products if not exists
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name='products' AND column_name='unit_of_measure_id'
    ) THEN
        ALTER TABLE products ADD COLUMN unit_of_measure_id UUID REFERENCES unit_of_measures(id);
        
        -- Migrate existing products based on is_weighable
        UPDATE products 
        SET unit_of_measure_id = CASE 
            WHEN is_weighable = TRUE THEN 'a0000000-0000-0000-0000-000000000002'::UUID 
            ELSE 'a0000000-0000-0000-0000-000000000001'::UUID 
        END;
        
        -- Set not-null constraint
        ALTER TABLE products ALTER COLUMN unit_of_measure_id SET NOT NULL;
    END IF;
END $$;
