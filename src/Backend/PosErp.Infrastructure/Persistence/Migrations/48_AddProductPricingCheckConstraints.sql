-- 48_AddProductPricingCheckConstraints.sql
ALTER TABLE products
ADD CONSTRAINT CK_Product_SellingPrice CHECK (selling_price > 0),
ADD CONSTRAINT CK_Product_Mrp CHECK (mrp > 0),
ADD CONSTRAINT CK_Product_PurchasePrice CHECK (purchase_price >= 0),
ADD CONSTRAINT CK_Product_SellingPrice_MRP CHECK (selling_price <= mrp);
