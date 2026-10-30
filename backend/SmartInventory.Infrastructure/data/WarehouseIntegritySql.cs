namespace SmartInventory.Infrastructure.Data;

/// <summary>Migration SQL: validation-only deferred checks preserve EF-tracked aggregate values.</summary>
public static class WarehouseIntegritySql
{
    public const string SeedMain = """
        INSERT INTO warehouses (id, code, name, is_active, is_default, created_at, updated_at)
        VALUES (1, 'MAIN', 'Kho chính', TRUE, TRUE, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP);
        SELECT setval(pg_get_serial_sequence('warehouses', 'id'), GREATEST((SELECT MAX(id) FROM warehouses), 1), TRUE);
        INSERT INTO warehouse_inventory (product_id, warehouse_id, quantity, updated_at)
        SELECT product_id, 1, quantity, updated_at FROM inventory;
        """;

    public const string CreateConstraints = """
        CREATE FUNCTION validate_warehouse_inventory_sum() RETURNS trigger LANGUAGE plpgsql SECURITY INVOKER AS $function$
        DECLARE
            product_key integer;
            old_key integer;
            new_key integer;
            aggregate_quantity integer;
            local_quantity bigint;
            local_count bigint;
        BEGIN
            IF TG_OP <> 'INSERT' THEN old_key := OLD.product_id; END IF;
            IF TG_OP <> 'DELETE' THEN new_key := NEW.product_id; END IF;
            FOR product_key IN SELECT DISTINCT key FROM unnest(ARRAY[old_key, new_key]) AS key WHERE key IS NOT NULL ORDER BY key
            LOOP
                EXECUTE format('SELECT quantity FROM %I.inventory WHERE product_id = $1', TG_TABLE_SCHEMA)
                    INTO aggregate_quantity USING product_key;
                EXECUTE format('SELECT COALESCE(SUM(quantity::bigint), 0), COUNT(*) FROM %I.warehouse_inventory WHERE product_id = $1', TG_TABLE_SCHEMA)
                    INTO local_quantity, local_count USING product_key;
                IF local_quantity > 2147483647 OR local_quantity < 0 OR
                    (aggregate_quantity IS NULL AND local_count > 0) OR
                    (aggregate_quantity IS NOT NULL AND aggregate_quantity::bigint <> local_quantity) THEN
                    RAISE EXCEPTION 'Warehouse inventory and aggregate quantity must remain consistent.'
                        USING ERRCODE = '23514', CONSTRAINT = 'CK_inventory_warehouse_sum';
                END IF;
            END LOOP;
            RETURN NULL;
        END;
        $function$;
        CREATE CONSTRAINT TRIGGER CK_inventory_warehouse_sum
            AFTER INSERT OR UPDATE OR DELETE ON inventory DEFERRABLE INITIALLY DEFERRED
            FOR EACH ROW EXECUTE FUNCTION validate_warehouse_inventory_sum();
        CREATE CONSTRAINT TRIGGER CK_warehouse_inventory_sum
            AFTER INSERT OR UPDATE OR DELETE ON warehouse_inventory DEFERRABLE INITIALLY DEFERRED
            FOR EACH ROW EXECUTE FUNCTION validate_warehouse_inventory_sum();
        """;

    public const string DropConstraints = """
        DROP TRIGGER IF EXISTS CK_inventory_warehouse_sum ON inventory;
        DROP TRIGGER IF EXISTS CK_warehouse_inventory_sum ON warehouse_inventory;
        DROP FUNCTION IF EXISTS validate_warehouse_inventory_sum();
        """;
}
