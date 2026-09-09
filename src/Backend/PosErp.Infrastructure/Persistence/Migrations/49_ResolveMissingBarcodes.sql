-- ════════════════════════════════════════════════════════════════════════════════
-- Migration 49: ResolveMissingBarcodes
-- Auto-seeds customer-reported missing barcodes in Apple Supermarket POS:
--   1. 8901063365131 -> Britannia Gobbles Red Velvet Marble Cake 110g (MRP ₹30 / SP ₹29)
--   2. 8901063368590 -> Britannia Muffills Strawberry Cake 35g (MRP ₹10 / SP ₹9.50)
--   3. 8901063025509 -> Britannia Milk Bikis Classic 114.4g (MRP ₹20 / SP ₹19)
--   4. 8901063365933 -> Britannia Gobbles Fruity Fun Cake 100g (MRP ₹30 / SP ₹29)
--   5. 8901063012998 -> Britannia Milk Bikis Biscuits 76g (MRP ₹10 / SP ₹9.50)
-- ════════════════════════════════════════════════════════════════════════════════

DO $$
DECLARE
    v_store_id uuid;
    v_category_id uuid;
    v_tax_slab_id uuid;
    v_uom_id uuid;

    v_p1_id uuid;
    v_b1_id uuid;
    v_bt1_id uuid;

    v_p2_id uuid;
    v_b2_id uuid;
    v_bt2_id uuid;

    v_p3_id uuid;
    v_b3_id uuid;
    v_bt3_id uuid;

    v_p4_id uuid;
    v_b4_id uuid;
    v_bt4_id uuid;

    v_p5_id uuid;
    v_b5_id uuid;
    v_bt5_id uuid;
BEGIN
    -- 1. Resolve Store (Primary store or default head office)
    SELECT id INTO v_store_id FROM stores WHERE is_deleted = false ORDER BY created_at ASC LIMIT 1;
    IF v_store_id IS NULL THEN
        v_store_id := '00000000-0000-0000-0000-000000000000';
        INSERT INTO stores (id, store_code, store_name, is_active, is_deleted, created_at)
        VALUES (v_store_id, 'STORE-01', 'Apple Supermarket Head Office', true, false, NOW())
        ON CONFLICT (id) DO NOTHING;
    END IF;

    -- 2. Resolve Unit of Measure (Pcs/Pieces)
    SELECT id INTO v_uom_id FROM unit_of_measures WHERE symbol ILIKE '%pcs%' OR name ILIKE '%pieces%' LIMIT 1;
    IF v_uom_id IS NULL THEN
        SELECT id INTO v_uom_id FROM unit_of_measures WHERE is_deleted = false LIMIT 1;
    END IF;
    IF v_uom_id IS NULL THEN
        v_uom_id := gen_random_uuid();
        INSERT INTO unit_of_measures (id, name, symbol, is_deleted, created_at)
        VALUES (v_uom_id, 'Pieces', 'Pcs', false, NOW());
    END IF;

    -- 3. Resolve Category (General / Bakery / FMCG)
    SELECT id INTO v_category_id FROM categories WHERE name ILIKE '%general%' OR name ILIKE '%bakery%' OR is_deleted = false LIMIT 1;
    IF v_category_id IS NULL THEN
        v_category_id := gen_random_uuid();
        INSERT INTO categories (id, name, is_deleted, created_at)
        VALUES (v_category_id, 'General / FMCG', false, NOW());
    END IF;

    -- 4. Resolve Tax Slab (GST 0%)
    SELECT id INTO v_tax_slab_id FROM tax_slabs WHERE cgst_rate = 0 AND sgst_rate = 0 AND is_deleted = false LIMIT 1;
    IF v_tax_slab_id IS NULL THEN
        SELECT id INTO v_tax_slab_id FROM tax_slabs WHERE is_deleted = false LIMIT 1;
    END IF;
    IF v_tax_slab_id IS NULL THEN
        v_tax_slab_id := gen_random_uuid();
        INSERT INTO tax_slabs (id, name, cgst_rate, sgst_rate, igst_rate, cess_rate, is_deleted, created_at)
        VALUES (v_tax_slab_id, 'GST 0%', 0, 0, 0, 0, false, NOW());
    END IF;

    -- ════════════════════════════════════════════════════════════════════════════
    -- ITEM 1: 8901063365131 - Britannia Gobbles Red Velvet Marble Cake 110g (₹30)
    -- Code: BA-36606
    -- ════════════════════════════════════════════════════════════════════════════
    SELECT p.id INTO v_p1_id
    FROM products p
    LEFT JOIN barcodes b ON b.product_id = p.id AND b.barcode = '8901063365131'
    WHERE p.product_code = 'BA-36606' OR b.id IS NOT NULL
    LIMIT 1;

    IF v_p1_id IS NULL THEN
        v_p1_id := gen_random_uuid();
        INSERT INTO products (
            id, store_id, product_code, name, tamil_name, description,
            category_id, tax_slab_id, unit_of_measure_id, is_weighable, has_expiry,
            mrp, selling_price, purchase_price, current_stock, is_active, is_deleted, created_at
        ) VALUES (
            v_p1_id, v_store_id, 'BA-36606',
            'BRITANNIA GOBBLES RED VELVET 110G',
            'பிரிட்டானியா கோபில்ஸ் ரெட் வெல்வெட் 110G',
            'Britannia Gobbles Red Velvet Flavoured Marble Cake 110g',
            v_category_id, v_tax_slab_id, v_uom_id, false, true,
            30.0000, 29.0000, 24.0000, 50.0000, true, false, NOW()
        );
    ELSE
        UPDATE products SET
            name = 'BRITANNIA GOBBLES RED VELVET 110G',
            tamil_name = 'பிரிட்டானியா கோபில்ஸ் ரெட் வெல்வெட் 110G',
            mrp = 30.0000,
            selling_price = 29.0000,
            purchase_price = 24.0000,
            current_stock = GREATEST(current_stock, 50.0000),
            is_active = true,
            is_deleted = false
        WHERE id = v_p1_id;
    END IF;

    SELECT id INTO v_b1_id FROM barcodes WHERE barcode = '8901063365131' LIMIT 1;
    IF v_b1_id IS NULL THEN
        INSERT INTO barcodes (id, store_id, product_id, barcode, is_primary, created_at, is_deleted)
        VALUES (gen_random_uuid(), v_store_id, v_p1_id, '8901063365131', true, NOW(), false);
    ELSE
        UPDATE barcodes SET product_id = v_p1_id, is_primary = true, is_deleted = false WHERE id = v_b1_id;
    END IF;

    SELECT id INTO v_bt1_id FROM product_batches WHERE product_id = v_p1_id AND batch_number = 'BAT-36606' LIMIT 1;
    IF v_bt1_id IS NULL THEN
        INSERT INTO product_batches (id, store_id, product_id, batch_number, mrp, cost_price, available_quantity, is_active, created_at)
        VALUES (gen_random_uuid(), v_store_id, v_p1_id, 'BAT-36606', 30.0000, 24.0000, 50.0000, true, NOW());
    ELSE
        UPDATE product_batches SET available_quantity = GREATEST(available_quantity, 50.0000), is_active = true WHERE id = v_bt1_id;
    END IF;

    -- ════════════════════════════════════════════════════════════════════════════
    -- ITEM 2: 8901063368590 - Britannia Muffills Strawberry Cake 35g (₹10)
    -- Code: BA-36607
    -- ════════════════════════════════════════════════════════════════════════════
    SELECT p.id INTO v_p2_id
    FROM products p
    LEFT JOIN barcodes b ON b.product_id = p.id AND b.barcode = '8901063368590'
    WHERE p.product_code = 'BA-36607' OR b.id IS NOT NULL
    LIMIT 1;

    IF v_p2_id IS NULL THEN
        v_p2_id := gen_random_uuid();
        INSERT INTO products (
            id, store_id, product_code, name, tamil_name, description,
            category_id, tax_slab_id, unit_of_measure_id, is_weighable, has_expiry,
            mrp, selling_price, purchase_price, current_stock, is_active, is_deleted, created_at
        ) VALUES (
            v_p2_id, v_store_id, 'BA-36607',
            'BRITANNIA MUFFILLS STRAWBERRY 10',
            'பிரிட்டானியா மஃபில்ஸ் ஸ்ட்ராபெரி 10',
            'Britannia Muffills Strawberry Center Filled Muffin Cake 35g',
            v_category_id, v_tax_slab_id, v_uom_id, false, true,
            10.0000, 9.5000, 8.0000, 50.0000, true, false, NOW()
        );
    ELSE
        UPDATE products SET
            name = 'BRITANNIA MUFFILLS STRAWBERRY 10',
            tamil_name = 'பிரிட்டானியா மஃபில்ஸ் ஸ்ட்ராபெரி 10',
            mrp = 10.0000,
            selling_price = 9.5000,
            purchase_price = 8.0000,
            current_stock = GREATEST(current_stock, 50.0000),
            is_active = true,
            is_deleted = false
        WHERE id = v_p2_id;
    END IF;

    SELECT id INTO v_b2_id FROM barcodes WHERE barcode = '8901063368590' LIMIT 1;
    IF v_b2_id IS NULL THEN
        INSERT INTO barcodes (id, store_id, product_id, barcode, is_primary, created_at, is_deleted)
        VALUES (gen_random_uuid(), v_store_id, v_p2_id, '8901063368590', true, NOW(), false);
    ELSE
        UPDATE barcodes SET product_id = v_p2_id, is_primary = true, is_deleted = false WHERE id = v_b2_id;
    END IF;

    SELECT id INTO v_bt2_id FROM product_batches WHERE product_id = v_p2_id AND batch_number = 'BAT-36607' LIMIT 1;
    IF v_bt2_id IS NULL THEN
        INSERT INTO product_batches (id, store_id, product_id, batch_number, mrp, cost_price, available_quantity, is_active, created_at)
        VALUES (gen_random_uuid(), v_store_id, v_p2_id, 'BAT-36607', 10.0000, 8.0000, 50.0000, true, NOW());
    ELSE
        UPDATE product_batches SET available_quantity = GREATEST(available_quantity, 50.0000), is_active = true WHERE id = v_bt2_id;
    END IF;

    -- ════════════════════════════════════════════════════════════════════════════
    -- ITEM 3: 8901063025509 - Britannia Milk Bikis Classic 114.4g (₹20)
    -- Linked to BA-22345
    -- ════════════════════════════════════════════════════════════════════════════
    SELECT id INTO v_p3_id FROM products WHERE product_code = 'BA-22345' AND is_deleted = false LIMIT 1;
    IF v_p3_id IS NULL THEN
        v_p3_id := gen_random_uuid();
        INSERT INTO products (
            id, store_id, product_code, name, tamil_name, description,
            category_id, tax_slab_id, unit_of_measure_id, is_weighable, has_expiry,
            mrp, selling_price, purchase_price, current_stock, is_active, is_deleted, created_at
        ) VALUES (
            v_p3_id, v_store_id, 'BA-22345',
            'BRITANNIA MILK BIKIS 20 (114.4G)',
            'பிரிட்டானியா மில்க் பிகிஸ் 20',
            'Britannia Milk Bikis Classic Biscuits 114.4g',
            v_category_id, v_tax_slab_id, v_uom_id, false, true,
            20.0000, 19.0000, 16.5000, 50.0000, true, false, NOW()
        );
    ELSE
        UPDATE products SET
            name = 'BRITANNIA MILK BIKIS 20 (114.4G)',
            tamil_name = 'பிரிட்டானியா மில்க் பிகிஸ் 20',
            mrp = 20.0000,
            selling_price = 19.0000,
            current_stock = GREATEST(current_stock, 50.0000),
            is_active = true,
            is_deleted = false
        WHERE id = v_p3_id;
    END IF;

    UPDATE barcodes SET is_primary = false WHERE product_id = v_p3_id;

    SELECT id INTO v_b3_id FROM barcodes WHERE barcode = '8901063025509' LIMIT 1;
    IF v_b3_id IS NULL THEN
        INSERT INTO barcodes (id, store_id, product_id, barcode, is_primary, created_at, is_deleted)
        VALUES (gen_random_uuid(), v_store_id, v_p3_id, '8901063025509', true, NOW(), false);
    ELSE
        UPDATE barcodes SET product_id = v_p3_id, is_primary = true, is_deleted = false WHERE id = v_b3_id;
    END IF;

    SELECT id INTO v_bt3_id FROM product_batches WHERE product_id = v_p3_id AND batch_number = 'BAT-22345' LIMIT 1;
    IF v_bt3_id IS NULL THEN
        INSERT INTO product_batches (id, store_id, product_id, batch_number, mrp, cost_price, available_quantity, is_active, created_at)
        VALUES (gen_random_uuid(), v_store_id, v_p3_id, 'BAT-22345', 20.0000, 16.5000, 50.0000, true, NOW());
    ELSE
        UPDATE product_batches SET available_quantity = GREATEST(available_quantity, 50.0000), is_active = true WHERE id = v_bt3_id;
    END IF;

    -- ════════════════════════════════════════════════════════════════════════════
    -- ITEM 4: 8901063365933 - Britannia Gobbles Fruity Fun Cake 100g (₹30)
    -- Code: BA-36608
    -- ════════════════════════════════════════════════════════════════════════════
    SELECT p.id INTO v_p4_id
    FROM products p
    LEFT JOIN barcodes b ON b.product_id = p.id AND b.barcode = '8901063365933'
    WHERE p.product_code = 'BA-36608' OR b.id IS NOT NULL
    LIMIT 1;

    IF v_p4_id IS NULL THEN
        v_p4_id := gen_random_uuid();
        INSERT INTO products (
            id, store_id, product_code, name, tamil_name, description,
            category_id, tax_slab_id, unit_of_measure_id, is_weighable, has_expiry,
            mrp, selling_price, purchase_price, current_stock, is_active, is_deleted, created_at
        ) VALUES (
            v_p4_id, v_store_id, 'BA-36608',
            'BRITANNIA GOBBLES FRUITY FUN 100G',
            'பிரிட்டானியா கோபில்ஸ் ஃப்ரூட்டி கேக் 100G',
            'Britannia Gobbles Fruity Fun Cake 100g',
            v_category_id, v_tax_slab_id, v_uom_id, false, true,
            30.0000, 29.0000, 24.0000, 50.0000, true, false, NOW()
        );
    ELSE
        UPDATE products SET
            name = 'BRITANNIA GOBBLES FRUITY FUN 100G',
            tamil_name = 'பிரிட்டானியா கோபில்ஸ் ஃப்ரூட்டி கேக் 100G',
            mrp = 30.0000,
            selling_price = 29.0000,
            purchase_price = 24.0000,
            current_stock = GREATEST(current_stock, 50.0000),
            is_active = true,
            is_deleted = false
        WHERE id = v_p4_id;
    END IF;

    SELECT id INTO v_b4_id FROM barcodes WHERE barcode = '8901063365933' LIMIT 1;
    IF v_b4_id IS NULL THEN
        INSERT INTO barcodes (id, store_id, product_id, barcode, is_primary, created_at, is_deleted)
        VALUES (gen_random_uuid(), v_store_id, v_p4_id, '8901063365933', true, NOW(), false);
    ELSE
        UPDATE barcodes SET product_id = v_p4_id, is_primary = true, is_deleted = false WHERE id = v_b4_id;
    END IF;

    SELECT id INTO v_bt4_id FROM product_batches WHERE product_id = v_p4_id AND batch_number = 'BAT-36608' LIMIT 1;
    IF v_bt4_id IS NULL THEN
        INSERT INTO product_batches (id, store_id, product_id, batch_number, mrp, cost_price, available_quantity, is_active, created_at)
        VALUES (gen_random_uuid(), v_store_id, v_p4_id, 'BAT-36608', 30.0000, 24.0000, 50.0000, true, NOW());
    ELSE
        UPDATE product_batches SET available_quantity = GREATEST(available_quantity, 50.0000), is_active = true WHERE id = v_bt4_id;
    END IF;

    -- ════════════════════════════════════════════════════════════════════════════
    -- ITEM 5: 8901063012998 - Britannia Milk Bikis Biscuits 76g (₹10)
    -- Linked to BA-5675
    -- ════════════════════════════════════════════════════════════════════════════
    SELECT id INTO v_p5_id FROM products WHERE product_code = 'BA-5675' AND is_deleted = false LIMIT 1;
    IF v_p5_id IS NULL THEN
        v_p5_id := gen_random_uuid();
        INSERT INTO products (
            id, store_id, product_code, name, tamil_name, description,
            category_id, tax_slab_id, unit_of_measure_id, is_weighable, has_expiry,
            mrp, selling_price, purchase_price, current_stock, is_active, is_deleted, created_at
        ) VALUES (
            v_p5_id, v_store_id, 'BA-5675',
            'BRITANNIA MILK BIKIS 10 (76G)',
            'பிரிட்டானியா மில்க் பிகிஸ் 10',
            'Britannia Milk Bikis Biscuits 76g',
            v_category_id, v_tax_slab_id, v_uom_id, false, true,
            10.0000, 9.5000, 8.2000, 50.0000, true, false, NOW()
        );
    ELSE
        UPDATE products SET
            name = 'BRITANNIA MILK BIKIS 10 (76G)',
            tamil_name = 'பிரிட்டானியா மில்க் பிகிஸ் 10',
            mrp = 10.0000,
            selling_price = 9.5000,
            current_stock = GREATEST(current_stock, 50.0000),
            is_active = true,
            is_deleted = false
        WHERE id = v_p5_id;
    END IF;

    UPDATE barcodes SET is_primary = false WHERE product_id = v_p5_id;

    SELECT id INTO v_b5_id FROM barcodes WHERE barcode = '8901063012998' LIMIT 1;
    IF v_b5_id IS NULL THEN
        INSERT INTO barcodes (id, store_id, product_id, barcode, is_primary, created_at, is_deleted)
        VALUES (gen_random_uuid(), v_store_id, v_p5_id, '8901063012998', true, NOW(), false);
    ELSE
        UPDATE barcodes SET product_id = v_p5_id, is_primary = true, is_deleted = false WHERE id = v_b5_id;
    END IF;

    SELECT id INTO v_bt5_id FROM product_batches WHERE product_id = v_p5_id AND batch_number = 'BAT-5675' LIMIT 1;
    IF v_bt5_id IS NULL THEN
        INSERT INTO product_batches (id, store_id, product_id, batch_number, mrp, cost_price, available_quantity, is_active, created_at)
        VALUES (gen_random_uuid(), v_store_id, v_p5_id, 'BAT-5675', 10.0000, 8.2000, 50.0000, true, NOW());
    ELSE
        UPDATE product_batches SET available_quantity = GREATEST(available_quantity, 50.0000), is_active = true WHERE id = v_bt5_id;
    END IF;

END $$;
