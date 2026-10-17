# Data model

Design for the Postgres schema (Phase 1). Decisions: ADR-0002 (init scripts), ADR-0007 (card
data), ADR-0011 (toggles: dual columns, separate flag database, no superuser connections).

General choices:
- **Primary keys:** sequential integers. Order numbers start at 1001, so object IDs are easy to
  guess. That makes object-level authorisation (TH4) easy to demonstrate; the secure mode adds an
  ownership check rather than hiding the IDs.
- **Money:** `numeric(10,2)`. Never floating point.
- **Status fields:** `text` with a `CHECK` list (easier to extend than a Postgres enum).
- **Timestamps:** `timestamptz`, stored in UTC.
- **Free-text fields** (display names, order notes, reviews) are deliberate carriers for stored
  XSS and indirect prompt injection (T7). They're marked ✎ below.

## Database `duckstore`

| Table | Purpose | Key columns | Threat links |
|-------|---------|-------------|--------------|
| `users` | Customers and staff in one table | `id`, `email` (unique), `display_name` ✎, `role` (`customer` / `employee` / `admin` / `auditor`), `password_hash_legacy` (insecure mode), `password_hash` (secure mode), `created_at` | A4, A17, TH7 |
| `user_identities` | Social logins linked to a user (Phase 5) | `user_id`, `provider` (`google` / `meta`), `subject`, `email`, `email_verified`, `linked_at` | A12, TB3 |
| `products` | The ducks | `id`, `sku`, `name`, `description`, `price`, `stock`, `image_url` | TH2 |
| `product_reviews` | Customer reviews | `product_id`, `user_id`, `rating` (1–5), `body` ✎, `created_at` | T7, TH8 |
| `orders` | One per checkout | `id` (from 1001), `user_id`, `status` (`placed` / `paid` / `shipped` / `delivered` / `cancelled`), `notes` ✎, `shipping_address`, `created_at`, `updated_at`, `shipped_at` | TH4, business-logic threats |
| `order_items` | Lines on an order | `order_id`, `product_id`, `quantity`, `unit_price` (captured at order time) | TH2 |
| `invoices` | One per order | `order_id`, `amount_due`, `status` (`open` / `paid` / `void`), `due_date`, `paid_at` | A14, TH30 |
| `payments` | Payment attempts | `invoice_id`, `amount`, `provider_ref`, `status`, `created_at` | A14, TH29 |
| `saved_cards` | "Remember my card" | Insecure mode: full card details (ADR-0007). Secure mode: `payment_token`. Both: `last4`, `expiry_month`, `expiry_year` | A2 |
| `audit_log` | Security-relevant events | `occurred_at`, `user_id`, `action`, `object_type`, `object_id`, `source_ip`, `trace_id`, `details` (jsonb) | TH3, TH12. **Created but left unused in the alpha** |

**Dual columns (ADR-0011):** for passwords and cards, the seed fills both the insecure and secure
forms, so the flag can switch instantly. The range-control "purge insecure data" action clears
the insecure columns. Seeded card data uses **only payment-provider published test numbers**.

## Database `range_control`

Separate database. Store roles have no CONNECT privilege on it.

| Table | Purpose | Key columns |
|-------|---------|-------------|
| `defence_flags` | One row per vulnerability ID | `vuln_id`, `title`, `kind` (`code` / `infra` / `data`), `mode` (`vulnerable` / `secure`), `requires_restart`, `updated_at`, `updated_by` |
| `presets` / `preset_flags` | Named sets of flag values | `preset_id`, `name`; `preset_id`, `vuln_id`, `mode` |
| `flag_changes` | History of every change (also sent to telemetry) | `changed_at`, `vuln_id`, `old_mode`, `new_mode`, `changed_by` |

## Roles

| Role | Used by | Privileges |
|------|---------|------------|
| `duckadmin` (`POSTGRES_USER`) | Init scripts and humans only | Superuser. **No service ever connects as this.** |
| `store_owner` | store-api in *vulnerable* DB-role mode | Owns all `duckstore` objects. Over-privileged, but not a superuser |
| `store_app` | store-api in *secure* DB-role mode | Only the DML each table needs |
| `range_control_owner` | range-control | Owns `range_control` |
| `flags_reader` | All range services | `SELECT` on `range_control.defence_flags` only |

## Seed data (deterministic)

- **Personas:** Alice and Bob (customers), Eve (employee), Mallory (admin), Trent (auditor).
- **Products:** 10–12 rubber ducks.
- **Orders:** several per customer, covering every status, with invoices in each state.
- **Defence flags:** one row per planned vuln ID, all `vulnerable`, plus the *all vulnerable* and
  *all secure* presets.

## Database guards (constraints as toggles)

A database constraint would block some tampering and business-logic attacks whatever the
app-level flag says. Rather than lose those demonstrations, the key constraints are **guards**:
a second, independent layer of defence that range-control switches on or off (a `data`-kind flag).

- Guards are named constraints or triggers, added or removed by `set_guard(name, enabled)`. It's a
  `SECURITY DEFINER` function with a fixed allow-list of guard names (no dynamic SQL from input),
  executable only by `range_control_owner`.
- Check constraints are added `NOT VALID`, so they apply to new rows without rejecting existing data.
- Guards start **off** (vulnerable). Turning on both the app-level fix and the guard shows defence in depth.

| Guard | Defends against |
|-------|-----------------|
| `order_items_quantity_positive` | Zero or negative quantities (TH2) |
| `order_items_unit_price_nonnegative` | Negative prices (TH2) |
| `invoices_amount_nonnegative` | Negative amounts due (TH2) |
| `payments_amount_positive` | Zero or negative payments (TH2, TH30) |
| `orders_no_cancel_after_ship` | Cancelling an order that has shipped or been delivered (business logic) |

Constraints that don't neutralise a planned vulnerability (primary and foreign keys, unique emails,
status value lists) are always on.
