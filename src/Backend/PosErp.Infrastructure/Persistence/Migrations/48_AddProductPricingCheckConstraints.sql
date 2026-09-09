-- 48_AddProductPricingCheckConstraints.sql
UPDATE products SET selling_price = 1.0, mrp = 1.0 WHERE selling_price <= 0 OR mrp <= 0;
UPDATE products SET mrp = selling_price WHERE selling_price > mrp;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_product_sellingprice') THEN
        ALTER TABLE products ADD CONSTRAINT CK_Product_SellingPrice CHECK (selling_price > 0);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_product_mrp') THEN
        ALTER TABLE products ADD CONSTRAINT CK_Product_Mrp CHECK (mrp > 0);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_product_purchaseprice') THEN
        ALTER TABLE products ADD CONSTRAINT CK_Product_PurchasePrice CHECK (purchase_price >= 0);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_product_sellingprice_mrp') THEN
        ALTER TABLE products ADD CONSTRAINT CK_Product_SellingPrice_MRP CHECK (selling_price <= mrp);
    END IF;
END $$;

