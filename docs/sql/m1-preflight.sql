-- Read-only checks before applying the M1 schema integrity migration.
-- Run against the existing ai_smart_inventory database, for example:
-- psql -v ON_ERROR_STOP=1 -d ai_smart_inventory -f docs/sql/m1-preflight.sql
-- Every SELECT should return zero rows before applying the migration.
-- Investigate and resolve reported records without deleting business data.
-- SKU and username comparisons match the case-sensitive unique indexes.

BEGIN TRANSACTION READ ONLY;

SELECT sku, COUNT(*) AS duplicate_count, ARRAY_AGG(id ORDER BY id) AS product_ids
FROM products
GROUP BY sku
HAVING COUNT(*) > 1
ORDER BY sku;

SELECT username, COUNT(*) AS duplicate_count, ARRAY_AGG(id ORDER BY id) AS user_ids
FROM users
GROUP BY username
HAVING COUNT(*) > 1
ORDER BY username;

SELECT product.id AS product_id, product.category_id
FROM products AS product
LEFT JOIN categories AS category ON category.id = product.category_id
WHERE category.id IS NULL
ORDER BY product.id;

SELECT inventory.product_id, inventory.quantity
FROM inventory
LEFT JOIN products AS product ON product.id = inventory.product_id
WHERE product.id IS NULL
ORDER BY inventory.product_id;

SELECT movement.id AS movement_id, movement.product_id
FROM stock_movements AS movement
LEFT JOIN products AS product ON product.id = movement.product_id
WHERE product.id IS NULL
ORDER BY movement.id;

SELECT product_id, quantity
FROM inventory
WHERE quantity < 0
ORDER BY product_id;

SELECT id AS product_id, sku, price
FROM products
WHERE price < 0
ORDER BY id;

SELECT id AS product_id, sku, min_stock
FROM products
WHERE min_stock < 0
ORDER BY id;

COMMIT;
