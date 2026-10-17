-- Database guards: toggleable second-layer defences (docs/data-model.md, ADR-0011 option 3).
-- All guards start OFF (vulnerable). range-control turns them on/off with set_guard().

SET ROLE store_owner;

-- Trigger function for the business-rule guard. The trigger is created disabled.
CREATE FUNCTION guard_no_cancel_after_ship() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    IF NEW.status = 'cancelled' AND OLD.status IN ('shipped', 'delivered') THEN
        RAISE EXCEPTION 'guard orders_no_cancel_after_ship: order % is already %', OLD.id, OLD.status;
    END IF;
    RETURN NEW;
END;
$$;

CREATE TRIGGER orders_no_cancel_after_ship
    BEFORE UPDATE OF status ON orders
    FOR EACH ROW EXECUTE FUNCTION guard_no_cancel_after_ship();
ALTER TABLE orders DISABLE TRIGGER orders_no_cancel_after_ship;

-- Turns one guard on or off. Fixed allow-list of guard names: no SQL is built from input.
-- SECURITY DEFINER runs as store_owner (the table owner); search_path is pinned for safety.
CREATE FUNCTION set_guard(guard text, enabled boolean) RETURNS void
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
BEGIN
    CASE guard
    WHEN 'order_items_quantity_positive' THEN
        ALTER TABLE order_items DROP CONSTRAINT IF EXISTS order_items_quantity_positive;
        IF enabled THEN
            ALTER TABLE order_items ADD CONSTRAINT order_items_quantity_positive
                CHECK (quantity > 0) NOT VALID;
        END IF;
    WHEN 'order_items_unit_price_nonnegative' THEN
        ALTER TABLE order_items DROP CONSTRAINT IF EXISTS order_items_unit_price_nonnegative;
        IF enabled THEN
            ALTER TABLE order_items ADD CONSTRAINT order_items_unit_price_nonnegative
                CHECK (unit_price >= 0) NOT VALID;
        END IF;
    WHEN 'invoices_amount_nonnegative' THEN
        ALTER TABLE invoices DROP CONSTRAINT IF EXISTS invoices_amount_nonnegative;
        IF enabled THEN
            ALTER TABLE invoices ADD CONSTRAINT invoices_amount_nonnegative
                CHECK (amount_due >= 0) NOT VALID;
        END IF;
    WHEN 'payments_amount_positive' THEN
        ALTER TABLE payments DROP CONSTRAINT IF EXISTS payments_amount_positive;
        IF enabled THEN
            ALTER TABLE payments ADD CONSTRAINT payments_amount_positive
                CHECK (amount > 0) NOT VALID;
        END IF;
    WHEN 'orders_no_cancel_after_ship' THEN
        IF enabled THEN
            ALTER TABLE orders ENABLE TRIGGER orders_no_cancel_after_ship;
        ELSE
            ALTER TABLE orders DISABLE TRIGGER orders_no_cancel_after_ship;
        END IF;
    ELSE
        RAISE EXCEPTION 'unknown guard: %', guard;
    END CASE;
END;
$$;

-- Current state of every guard, for the range-control panel.
CREATE VIEW guard_status AS
SELECT g.guard,
       CASE WHEN g.guard = 'orders_no_cancel_after_ship'
            THEN EXISTS (SELECT 1 FROM pg_trigger
                         WHERE tgname = g.guard AND tgenabled <> 'D')
            ELSE EXISTS (SELECT 1 FROM pg_constraint WHERE conname = g.guard)
       END AS enabled
FROM (VALUES ('order_items_quantity_positive'),
             ('order_items_unit_price_nonnegative'),
             ('invoices_amount_nonnegative'),
             ('payments_amount_positive'),
             ('orders_no_cancel_after_ship')) AS g(guard);

RESET ROLE;

-- Only range-control may toggle guards or read their state.
REVOKE EXECUTE ON FUNCTION set_guard(text, boolean) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION set_guard(text, boolean) TO range_control_owner;
GRANT SELECT ON guard_status TO range_control_owner;
