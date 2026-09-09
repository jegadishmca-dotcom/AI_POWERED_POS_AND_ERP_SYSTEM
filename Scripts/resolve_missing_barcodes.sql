-- ════════════════════════════════════════════════════════════════════════════════
-- SQL Script: resolve_missing_barcodes.sql
-- Resolves customer-reported missing barcodes in Apple Supermarket POS:
--   1. 8901063365131 -> Britannia Gobbles Red Velvet Marble Cake 110g (MRP ₹30 / SP ₹29)
--   2. 8901063368590 -> Britannia Muffills Strawberry Cake 35g (MRP ₹10 / SP ₹9.50)
--   3. 8901063025509 -> Britannia Milk Bikis Classic 114.4g (MRP ₹20 / SP ₹19)
--   4. 8901063365933 -> Britannia Gobbles Fruity Fun Cake 100g (MRP ₹30 / SP ₹29)
--   5. 8901063012998 -> Britannia Milk Bikis Biscuits 76g (MRP ₹10 / SP ₹9.50)
-- ════════════════════════════════════════════════════════════════════════════════

BEGIN;

DO $$
DECLARE
    v_store_id uuid := '00000000-0000-0000-0000-000000000000';
    v_category_id uuid;
    v_tax_slab_id uuid;
    v_uom_id uuid;
    v_p3_id uuid;
    v_p5_id uuid;
BEGIN
    -- Ensure Store exists
    INSERT INTO stores (id, store_code, store_name, is_active, is_deleted, created_at)
    VALUES (v_store_id, 'STORE-01', 'Apple Supermarket Head Office', true, false, NOW())
    ON CONFLICT (id) DO NOTHING;

    -- Ensure Unit of Measure exists
    SELECT id INTO v_uom_id FROM unit_of_measures WHERE symbol ILIKE '%pcs%' OR name ILIKE '%pieces%' LIMIT 1;
    IF v_uom_id IS NULL THEN
        v_uom_id := 'a0000000-0000-0000-0000-000000000001';
        INSERT INTO unit_of_measures (id, name, symbol, is_deleted, created_at)
        VALUES (v_uom_id, 'Pieces', 'Pcs', false, NOW())
        ON CONFLICT (id) DO NOTHING;
    END IF;

    -- Ensure Category exists
    SELECT id INTO v_category_id FROM categories WHERE name ILIKE '%general%' OR is_deleted = false LIMIT 1;
    IF v_category_id IS NULL THEN
        v_category_id := 'dea00e8f-08d9-4423-8e0e-9a7e28849ecb';
        INSERT INTO categories (id, name, is_deleted, created_at)
        VALUES (v_category_id, 'General / FMCG', false, NOW())
        ON CONFLICT (id) DO NOTHING;
    END IF;

    -- Ensure Tax Slab exists (GST 0%)
    SELECT id INTO v_tax_slab_id FROM tax_slabs WHERE cgst_rate = 0 AND sgst_rate = 0 AND is_deleted = false LIMIT 1;
    IF v_tax_slab_id IS NULL THEN
        v_tax_slab_id := '4e4ee36f-356e-17a9-872d-7f7373071a5e';
        INSERT INTO tax_slabs (id, name, cgst_rate, sgst_rate, igst_rate, cess_rate, is_deleted, created_at)
        VALUES (v_tax_slab_id, 'GST 0%', 0, 0, 0, 0, false, NOW())
        ON CONFLICT (id) DO NOTHING;
    END IF;

    -- ════════════════════════════════════════════════════════════════════════════
    -- ITEM 1: 8901063365131 - Britannia Gobbles Red Velvet Marble Cake 110g
    -- Code: BA-36606
    -- ════════════════════════════════════════════════════════════════════════════
    INSERT INTO products (
        id, store_id, product_code, name, tamil_name, description,
        category_id, tax_slab_id, unit_of_measure_id, is_weighable, has_expiry,
        mrp, selling_price, purchase_price, current_stock, is_active, is_deleted, created_at
    ) VALUES (
        '36606000-0000-0000-0000-890106336513',
        v_store_id,
        'BA-36606',
        'BRITANNIA GOBBLES RED VELVET 110G',
        'பிரிட்டானியா கோபில்ஸ் ரெட் வெல்வெட் 110G',
        'Britannia Gobbles Red Velvet Flavoured Marble Cake 110g',
        v_category_id,
        v_tax_slab_id,
        v_uom_id,
        false,
        true,
        30.0000,
        29.0000,
        24.0000,
        50.0000,
        true,
        false,
        NOW()
    ) ON CONFLICT (id) DO UPDATE SET
        name = EXCLUDED.name,
        tamil_name = EXCLUDED.tamil_name,
        mrp = EXCLUDED.mrp,
        selling_price = EXCLUDED.selling_price,
        purchase_price = EXCLUDED.purchase_price,
        current_stock = GREATEST(products.current_stock, 50.0000),
        is_active = true,
        is_deleted = false;

    -- Barcode for Item 1
    INSERT INTO barcodes (
        id, store_id, product_id, barcode, is_primary, created_at, is_deleted
    ) VALUES (
        '36606000-0000-0000-0001-890106336513',
        v_store_id,
        '36606000-0000-0000-0000-890106336513',
        '8901063365131',
        true,
        NOW(),
        false
    ) ON CONFLICT (id) DO UPDATE SET
        barcode = EXCLUDED.barcode,
        product_id = EXCLUDED.product_id,
        is_primary = true,
        is_deleted = false;

    -- Batch for Item 1
    INSERT INTO product_batches (
        id, store_id, product_id, batch_number, mrp, cost_price,
        available_quantity, is_active, created_at
    ) VALUES (
        '36606000-0000-0000-0002-890106336513',
        v_store_id,
        '36606000-0000-0000-0000-890106336513',
        'BAT-36606',
        30.0000,
        24.0000,
        50.0000,
        true,
        NOW()
    ) ON CONFLICT (id) DO UPDATE SET
        available_quantity = GREATEST(product_batches.available_quantity, 50.0000),
        is_active = true;

    -- ════════════════════════════════════════════════════════════════════════════
    -- ITEM 2: 8901063368590 - Britannia Muffills Strawberry Cake 35g (₹10)
    -- Code: BA-36607
    -- ════════════════════════════════════════════════════════════════════════════
    INSERT INTO products (
        id, store_id, product_code, name, tamil_name, description,
        category_id, tax_slab_id, unit_of_measure_id, is_weighable, has_expiry,
        mrp, selling_price, purchase_price, current_stock, is_active, is_deleted, created_at
    ) VALUES (
        '36607000-0000-0000-0000-890106336859',
        v_store_id,
        'BA-36607',
        'BRITANNIA MUFFILLS STRAWBERRY 10',
        'பிரிட்டானியா மஃபில்ஸ் ஸ்ட்ராபெரி 10',
        'Britannia Muffills Strawberry Center Filled Muffin Cake 35g',
        v_category_id,
        v_tax_slab_id,
        v_uom_id,
        false,
        true,
        10.0000,
        9.5000,
        8.0000,
        50.0000,
        true,
        false,
        NOW()
    ) ON CONFLICT (id) DO UPDATE SET
        name = EXCLUDED.name,
        tamil_name = EXCLUDED.tamil_name,
        mrp = EXCLUDED.mrp,
        selling_price = EXCLUDED.selling_price,
        purchase_price = EXCLUDED.purchase_price,
        current_stock = GREATEST(products.current_stock, 50.0000),
        is_active = true,
        is_deleted = false;

    -- Barcode for Item 2
    INSERT INTO barcodes (
        id, store_id, product_id, barcode, is_primary, created_at, is_deleted
    ) VALUES (
        '36607000-0000-0000-0001-890106336859',
        v_store_id,
        '36607000-0000-0000-0000-890106336859',
        '8901063368590',
        true,
        NOW(),
        false
    ) ON CONFLICT (id) DO UPDATE SET
        barcode = EXCLUDED.barcode,
        product_id = EXCLUDED.product_id,
        is_primary = true,
        is_deleted = false;

    -- Batch for Item 2
    INSERT INTO product_batches (
        id, store_id, product_id, batch_number, mrp, cost_price,
        available_quantity, is_active, created_at
    ) VALUES (
        '36607000-0000-0000-0002-890106336859',
        v_store_id,
        '36607000-0000-0000-0000-890106336859',
        'BAT-36607',
        10.0000,
        8.0000,
        50.0000,
        true,
        NOW()
    ) ON CONFLICT (id) DO UPDATE SET
        available_quantity = GREATEST(product_batches.available_quantity, 50.0000),
        is_active = true;

    -- ════════════════════════════════════════════════════════════════════════════
    -- ITEM 3: 8901063025509 - Britannia Milk Bikis Classic 114.4g (₹20)
    -- Link directly to existing BA-22345 (or create if absent)
    -- ════════════════════════════════════════════════════════════════════════════
    SELECT id INTO v_p3_id FROM products WHERE product_code = 'BA-22345' AND is_deleted = false LIMIT 1;
    IF v_p3_id IS NULL THEN
        v_p3_id := 'a07044e6-c0db-d181-a425-9e8dd7b7f964';
        INSERT INTO products (
            id, store_id, product_code, name, tamil_name, description,
            category_id, tax_slab_id, unit_of_measure_id, is_weighable, has_expiry,
            mrp, selling_price, purchase_price, current_stock, is_active, is_deleted, created_at
        ) VALUES (
            v_p3_id,
            v_store_id,
            'BA-22345',
            'BRITANNIA MILK BIKIS 20 (114.4G)',
            'பிரிட்டானியா மில்க் பிகிஸ் 20',
            'Britannia Milk Bikis Classic Biscuits 114.4g',
            v_category_id,
            v_tax_slab_id,
            v_uom_id,
            false,
            true,
            20.0000,
            19.0000,
            16.5000,
            50.0000,
            true,
            false,
            NOW()
        ) ON CONFLICT (id) DO UPDATE SET
            name = EXCLUDED.name,
            tamil_name = EXCLUDED.tamil_name,
            mrp = EXCLUDED.mrp,
            selling_price = EXCLUDED.selling_price,
            current_stock = GREATEST(products.current_stock, 50.0000),
            is_active = true,
            is_deleted = false;
    ELSE
        UPDATE products SET
            name = 'BRITANNIA MILK BIKIS 20 (114.4G)',
            tamil_name = 'பிரிட்டானியா மில்க் பிகிஸ் 20',
            mrp = 20.0000,
            selling_price = 19.0000,
            current_stock = GREATEST(current_stock, 50.0000),
            is_active = true
        WHERE id = v_p3_id;
    END IF;

    -- Set existing barcodes to secondary
    UPDATE barcodes SET is_primary = false WHERE product_id = v_p3_id;

    -- Insert or activate 8901063025509 as primary barcode
    INSERT INTO barcodes (
        id, store_id, product_id, barcode, is_primary, created_at, is_deleted
    ) VALUES (
        '30255000-0000-0000-0001-890106302550',
        v_store_id,
        v_p3_id,
        '8901063025509',
        true,
        NOW(),
        false
    ) ON CONFLICT (id) DO UPDATE SET
        barcode = EXCLUDED.barcode,
        product_id = v_p3_id,
        is_primary = true,
        is_deleted = false;

    -- Ensure batch with stock exists
    INSERT INTO product_batches (
        id, store_id, product_id, batch_number, mrp, cost_price,
        available_quantity, is_active, created_at
    ) VALUES (
        '30255000-0000-0000-0002-890106302550',
        v_store_id,
        v_p3_id,
        'BAT-22345',
        20.0000,
        16.5000,
        50.0000,
        true,
        NOW()
    ) ON CONFLICT (id) DO UPDATE SET
        available_quantity = GREATEST(product_batches.available_quantity, 50.0000),
        is_active = true;

    -- ════════════════════════════════════════════════════════════════════════════
    -- ITEM 4: 8901063365933 - Britannia Gobbles Fruity Fun Cake 100g (₹30)
    -- Code: BA-36608
    -- ════════════════════════════════════════════════════════════════════════════
    INSERT INTO products (
        id, store_id, product_code, name, tamil_name, description,
        category_id, tax_slab_id, unit_of_measure_id, is_weighable, has_expiry,
        mrp, selling_price, purchase_price, current_stock, is_active, is_deleted, created_at
    ) VALUES (
        '36608000-0000-0000-0000-890106336593',
        v_store_id,
        'BA-36608',
        'BRITANNIA GOBBLES FRUITY FUN 100G',
        'பிரிட்டானியா கோபில்ஸ் ஃப்ரூட்டி கேக் 100G',
        'Britannia Gobbles Fruity Fun Cake 100g',
        v_category_id,
        v_tax_slab_id,
        v_uom_id,
        false,
        true,
        30.0000,
        29.0000,
        24.0000,
        50.0000,
        true,
        false,
        NOW()
    ) ON CONFLICT (id) DO UPDATE SET
        name = EXCLUDED.name,
        tamil_name = EXCLUDED.tamil_name,
        mrp = EXCLUDED.mrp,
        selling_price = EXCLUDED.selling_price,
        purchase_price = EXCLUDED.purchase_price,
        current_stock = GREATEST(products.current_stock, 50.0000),
        is_active = true,
        is_deleted = false;

    -- Barcode for Item 4
    INSERT INTO barcodes (
        id, store_id, product_id, barcode, is_primary, created_at, is_deleted
    ) VALUES (
        '36608000-0000-0000-0001-890106336593',
        v_store_id,
        '36608000-0000-0000-0000-890106336593',
        '8901063365933',
        true,
        NOW(),
        false
    ) ON CONFLICT (id) DO UPDATE SET
        barcode = EXCLUDED.barcode,
        product_id = EXCLUDED.product_id,
        is_primary = true,
        is_deleted = false;

    -- Batch for Item 4
    INSERT INTO product_batches (
        id, store_id, product_id, batch_number, mrp, cost_price,
        available_quantity, is_active, created_at
    ) VALUES (
        '36608000-0000-0000-0002-890106336593',
        v_store_id,
        '36608000-0000-0000-0000-890106336593',
        'BAT-36608',
        30.0000,
        24.0000,
        50.0000,
        true,
        NOW()
    ) ON CONFLICT (id) DO UPDATE SET
        available_quantity = GREATEST(product_batches.available_quantity, 50.0000),
        is_active = true;

    -- ════════════════════════════════════════════════════════════════════════════
    -- ITEM 5: 8901063012998 - Britannia Milk Bikis Biscuits 76g (₹10)
    -- Link directly to existing BA-5675 (MILKBIKIS)
    -- ════════════════════════════════════════════════════════════════════════════
    SELECT id INTO v_p5_id FROM products WHERE product_code = 'BA-5675' AND is_deleted = false LIMIT 1;
    IF v_p5_id IS NULL THEN
        v_p5_id := '0f4b1473-4b3c-07ea-ae1d-2176aa176bb8';
        INSERT INTO products (
            id, store_id, product_code, name, tamil_name, description,
            category_id, tax_slab_id, unit_of_measure_id, is_weighable, has_expiry,
            mrp, selling_price, purchase_price, current_stock, is_active, is_deleted, created_at
        ) VALUES (
            v_p5_id,
            v_store_id,
            'BA-5675',
            'BRITANNIA MILK BIKIS 10 (76G)',
            'பிரிட்டானியா மில்க் பிகிஸ் 10',
            'Britannia Milk Bikis Biscuits 76g',
            v_category_id,
            v_tax_slab_id,
            v_uom_id,
            false,
            true,
            10.0000,
            9.5000,
            8.2000,
            50.0000,
            true,
            false,
            NOW()
        ) ON CONFLICT (id) DO UPDATE SET
            name = EXCLUDED.name,
            tamil_name = EXCLUDED.tamil_name,
            mrp = EXCLUDED.mrp,
            selling_price = EXCLUDED.selling_price,
            current_stock = GREATEST(products.current_stock, 50.0000),
            is_active = true,
            is_deleted = false;
    ELSE
        UPDATE products SET
            name = 'BRITANNIA MILK BIKIS 10 (76G)',
            tamil_name = 'பிரிட்டானியா மில்க் பிகிஸ் 10',
            mrp = 10.0000,
            selling_price = 9.5000,
            current_stock = GREATEST(current_stock, 50.0000),
            is_active = true
        WHERE id = v_p5_id;
    END IF;

    -- Set existing barcodes to secondary
    UPDATE barcodes SET is_primary = false WHERE product_id = v_p5_id;

    -- Insert or activate 8901063012998 as primary barcode
    INSERT INTO barcodes (
        id, store_id, product_id, barcode, is_primary, created_at, is_deleted
    ) VALUES (
        '30129000-0000-0000-0001-890106301299',
        v_store_id,
        v_p5_id,
        '8901063012998',
        true,
        NOW(),
        false
    ) ON CONFLICT (id) DO UPDATE SET
        barcode = EXCLUDED.barcode,
        product_id = v_p5_id,
        is_primary = true,
        is_deleted = false;

    -- Ensure batch with stock exists
    INSERT INTO product_batches (
        id, store_id, product_id, batch_number, mrp, cost_price,
        available_quantity, is_active, created_at
    ) VALUES (
        '30129000-0000-0000-0002-890106301299',
        v_store_id,
        v_p5_id,
        'BAT-5675',
        10.0000,
        8.2000,
        50.0000,
        true,
        NOW()
    ) ON CONFLICT (id) DO UPDATE SET
        available_quantity = GREATEST(product_batches.available_quantity, 50.0000),
        is_active = true;

END $$;

COMMIT;

-- ════════════════════════════════════════════════════════════════════════════
-- VERIFICATION REPORT: Check that all 5 barcodes now scan perfectly
-- ════════════════════════════════════════════════════════════════════════════
SELECT 
    b.barcode AS scanned_barcode,
    p.product_code,
    p.name AS product_name,
    p.mrp,
    p.selling_price,
    p.current_stock,
    b.is_primary,
    b.is_deleted AS barcode_deleted,
    p.is_active AS product_active
FROM barcodes b
JOIN products p ON b.product_id = p.id
WHERE b.barcode IN (
    '8901063365131',
    '8901063368590',
    '8901063025509',
    '8901063365933',
    '8901063012998'
)
ORDER BY b.barcode;
