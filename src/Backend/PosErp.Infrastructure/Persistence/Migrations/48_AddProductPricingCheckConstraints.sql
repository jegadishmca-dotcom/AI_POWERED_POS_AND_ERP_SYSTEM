-- 48_AddProductPricingCheckConstraints.sql
UPDATE products SET selling_price = 1.0, mrp = 1.0 WHERE selling_price <= 0 OR mrp <= 0;
ALTER TABLE products
ADD CONSTRAINT CK_Product_SellingPrice CHECK (selling_price > 0),
ADD CONSTRAINT CK_Product_Mrp CHECK (mrp > 0),
ADD CONSTRAINT CK_Product_PurchasePrice CHECK (purchase_price >= 0),
ADD CONSTRAINT CK_Product_SellingPrice_MRP CHECK (selling_price <= mrp);
