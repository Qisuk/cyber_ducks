-- Deterministic seed data (docs/data-model.md). Runs once on first start.
-- Fixed IDs and timestamps so demos and attack scripts are repeatable.
-- All people, addresses and emails are fictional (.test is a reserved domain).
-- Password columns are left NULL: store-api sets persona passwords from .env on first start
-- (Phase 3), because computing both stored forms is application logic behind a toggle.

SET ROLE store_owner;

-- Personas -------------------------------------------------------------------------------
INSERT INTO users (id, email, display_name, role, created_at) VALUES
  (1, 'alice@duckstore.test',   'Alice Anders',  'customer', '2026-08-01 09:00+00'),
  (2, 'bob@duckstore.test',     'Bob Brown',     'customer', '2026-08-03 14:20+00'),
  (3, 'carol@duckstore.test',   'Carol Chen',    'customer', '2026-08-10 11:05+00'),
  (4, 'dave@duckstore.test',    'Dave Dawson',   'customer', '2026-08-15 16:45+00'),
  (5, 'eve@duckstore.test',     'Eve Evans',     'employee', '2026-07-01 08:00+00'),
  (6, 'mallory@duckstore.test', 'Mallory Moss',  'admin',    '2026-07-01 08:00+00'),
  (7, 'trent@duckstore.test',   'Trent Taylor',  'auditor',  '2026-07-01 08:00+00');

-- The ducks ------------------------------------------------------------------------------
INSERT INTO products (id, sku, name, description, price, stock, image_url) VALUES
  (1,  'DUCK-CLASSIC',  'Classic Yellow Duck',  'The original. Squeaks reliably.',                       4.99,  250, '/images/ducks/classic.png'),
  (2,  'DUCK-DEBUG',    'Debugger Duck',        'Explain your code to it. It listens.',                  7.49,  120, '/images/ducks/debug.png'),
  (3,  'DUCK-PIRATE',   'Pirate Duck',          'Eye patch, hat, questionable licensing.',               8.99,   80, '/images/ducks/pirate.png'),
  (4,  'DUCK-NINJA',    'Ninja Duck',           'You never saw it arrive.',                              8.99,   75, '/images/ducks/ninja.png'),
  (5,  'DUCK-HOODIE',   'Hacker Duck',          'Hoodie up, terminal open.',                             9.99,   60, '/images/ducks/hacker.png'),
  (6,  'DUCK-FIREWALL', 'Firewall Duck',        'Blocks everything. Including bathwater.',               9.49,   40, '/images/ducks/firewall.png'),
  (7,  'DUCK-PHISH',    'Phishing Duck',        'Comes with a tiny fishing rod. Do not click its links.',6.99,   90, '/images/ducks/phishing.png'),
  (8,  'DUCK-HONEYPOT', 'Honeypot Duck',        'Sweet, sticky, and watching.',                          7.99,   55, '/images/ducks/honeypot.png'),
  (9,  'DUCK-CRYPTO',   'Encryption Duck',      'Its squeak is AES-256. Allegedly.',                    10.99,   35, '/images/ducks/encryption.png'),
  (10, 'DUCK-PATCH',    'Patch Tuesday Duck',   'Arrives monthly with fixes and new bugs.',              5.99,  150, '/images/ducks/patch.png'),
  (11, 'DUCK-ZERODAY',  'Zero-Day Duck',        'Limited edition. Nobody knew it existed.',             24.99,    5, '/images/ducks/zeroday.png'),
  (12, 'DUCK-GOLD',     'Golden Duck',          'For the bug bounty winner in your life.',              49.99,   10, '/images/ducks/golden.png');

-- Reviews (benign; attack content is planted by scenarios, never by the seed) -----------
INSERT INTO product_reviews (id, product_id, user_id, rating, body, created_at) VALUES
  (1, 2,  1, 5, 'Solved three bugs just by talking to it.',        '2026-09-02 10:00+00'),
  (2, 1,  2, 4, 'Classic for a reason. Squeak could be louder.',   '2026-09-05 18:30+00'),
  (3, 5,  3, 5, 'My whole team has one now.',                      '2026-09-12 12:15+00'),
  (4, 11, 4, 3, 'Great duck, but it sold out before I knew it existed.', '2026-09-20 09:40+00');

-- Orders: every status represented ---------------------------------------------------------
INSERT INTO orders (id, user_id, status, notes, shipping_address, created_at, updated_at, shipped_at) VALUES
  (1001, 1, 'delivered', 'Gift wrap please',          '1 Pond Lane, Duckton, DT1 1AA',    '2026-09-01 10:00+00', '2026-09-06 15:00+00', '2026-09-03 09:00+00'),
  (1002, 1, 'shipped',   '',                          '1 Pond Lane, Duckton, DT1 1AA',    '2026-10-05 13:30+00', '2026-10-08 09:00+00', '2026-10-08 09:00+00'),
  (1003, 1, 'placed',    'Leave with neighbour',      '1 Pond Lane, Duckton, DT1 1AA',    '2026-10-15 17:45+00', '2026-10-15 17:45+00', NULL),
  (1004, 2, 'paid',      '',                          '22 Quack Street, Billham, BH2 2BB', '2026-10-12 08:20+00', '2026-10-12 08:25+00', NULL),
  (1005, 2, 'cancelled', 'Ordered the wrong colour',  '22 Quack Street, Billham, BH2 2BB', '2026-09-18 19:00+00', '2026-09-19 10:00+00', NULL),
  (1006, 3, 'delivered', '',                          '3 Mallard Close, Featherby, FB3 3CC', '2026-08-25 11:10+00', '2026-08-30 14:00+00', '2026-08-27 08:30+00'),
  (1007, 3, 'placed',    'Office delivery, reception', '3 Mallard Close, Featherby, FB3 3CC', '2026-10-16 09:05+00', '2026-10-16 09:05+00', NULL),
  (1008, 4, 'shipped',   '',                          '4 Drake Road, Waddle, WD4 4DD',     '2026-10-09 15:50+00', '2026-10-11 10:00+00', '2026-10-11 10:00+00');

INSERT INTO order_items (id, order_id, product_id, quantity, unit_price) VALUES
  (1,  1001, 1, 3, 4.99),
  (2,  1001, 2, 1, 7.49),
  (3,  1002, 5, 2, 9.99),
  (4,  1003, 12, 1, 49.99),
  (5,  1003, 1, 2, 4.99),
  (6,  1004, 3, 1, 8.99),
  (7,  1004, 4, 1, 8.99),
  (8,  1005, 7, 4, 6.99),
  (9,  1006, 9, 1, 10.99),
  (10, 1006, 10, 2, 5.99),
  (11, 1007, 6, 5, 9.49),
  (12, 1008, 11, 1, 24.99),
  (13, 1008, 8, 1, 7.99);

-- Invoices: one per order. Totals match the order lines; status follows the order.
INSERT INTO invoices (id, order_id, amount_due, status, due_date, paid_at) VALUES
  (5001, 1001, 22.46, 'paid', '2026-09-15', '2026-09-01 10:05+00'),
  (5002, 1002, 19.98, 'paid', '2026-10-19', '2026-10-05 13:35+00'),
  (5003, 1003, 59.97, 'open', '2026-10-29', NULL),
  (5004, 1004, 17.98, 'paid', '2026-10-26', '2026-10-12 08:25+00'),
  (5005, 1005, 27.96, 'void', '2026-10-02', NULL),
  (5006, 1006, 22.97, 'paid', '2026-09-08', '2026-08-25 11:15+00'),
  (5007, 1007, 47.45, 'open', '2026-10-30', NULL),
  (5008, 1008, 32.98, 'paid', '2026-10-23', '2026-10-09 15:55+00');

INSERT INTO payments (id, invoice_id, amount, provider_ref, status, created_at) VALUES
  (1, 5001, 22.46, 'pay_seed_0001', 'succeeded', '2026-09-01 10:05+00'),
  (2, 5002, 19.98, 'pay_seed_0002', 'succeeded', '2026-10-05 13:35+00'),
  (3, 5003, 59.97, 'pay_seed_0003', 'failed',    '2026-10-15 17:50+00'),
  (4, 5004, 17.98, 'pay_seed_0004', 'succeeded', '2026-10-12 08:25+00'),
  (5, 5006, 22.97, 'pay_seed_0005', 'succeeded', '2026-08-25 11:15+00'),
  (6, 5008, 32.98, 'pay_seed_0006', 'succeeded', '2026-10-09 15:55+00');

-- Saved cards: payment-provider PUBLISHED TEST numbers only (they can never be charged).
-- Both forms are filled (ADR-0011 dual columns); the purge action clears card_number/card_cvv.
INSERT INTO saved_cards (id, user_id, cardholder_name, card_number, card_cvv, payment_token, last4, expiry_month, expiry_year, created_at) VALUES
  (1, 1, 'Alice Anders', '4242424242424242', '123', 'tok_seed_alice_visa', '4242', 12, 2029, '2026-09-01 10:04+00'),
  (2, 2, 'Bob Brown',    '5555555555554444', '321', 'tok_seed_bob_mc',     '4444',  6, 2028, '2026-10-12 08:24+00'),
  (3, 4, 'Dave Dawson',  '4000056655665556', '456', 'tok_seed_dave_debit', '5556',  3, 2030, '2026-10-09 15:54+00');

-- Explicit IDs were used above, so move each identity sequence past the seeded rows.
SELECT setval(pg_get_serial_sequence('users', 'id'),           (SELECT max(id) FROM users));
SELECT setval(pg_get_serial_sequence('products', 'id'),        (SELECT max(id) FROM products));
SELECT setval(pg_get_serial_sequence('product_reviews', 'id'), (SELECT max(id) FROM product_reviews));
SELECT setval(pg_get_serial_sequence('orders', 'id'),          (SELECT max(id) FROM orders));
SELECT setval(pg_get_serial_sequence('order_items', 'id'),     (SELECT max(id) FROM order_items));
SELECT setval(pg_get_serial_sequence('invoices', 'id'),        (SELECT max(id) FROM invoices));
SELECT setval(pg_get_serial_sequence('payments', 'id'),        (SELECT max(id) FROM payments));
SELECT setval(pg_get_serial_sequence('saved_cards', 'id'),     (SELECT max(id) FROM saved_cards));

RESET ROLE;

-- Defence flags (range_control) ------------------------------------------------------------
-- Provisional vuln IDs for the alpha; threat-model §3 will finalise the catalogue.
\connect range_control
SET ROLE range_control_owner;

INSERT INTO defence_flags (vuln_id, title, kind, requires_restart) VALUES
  ('VULN-IDOR-01',    'Object-level authorisation on orders and invoices',     'code', false),
  ('VULN-MASS-01',    'Allow-listed fields on profile updates',                'code', false),
  ('VULN-LOGIC-01',   'Server recomputes prices and totals',                   'code', false),
  ('VULN-LOGIC-02',   'Order state rules enforced server-side',                'code', false),
  ('VULN-SQLI-01',    'Parameterised queries',                                 'code', false),
  ('VULN-XSS-01',     'Output encoding of user content',                       'code', false),
  ('VULN-CSRF-01',    'Anti-forgery tokens',                                   'code', false),
  ('VULN-AUTH-01',    'Login throttling and lockout',                          'code', false),
  ('VULN-SESSION-01', 'Session and token handling',                            'code', false),
  ('VULN-INFO-01',    'Generic error responses',                               'code', false),
  ('VULN-DBROLE-01',  'store-api connects as least-privilege role',            'code', true),
  ('VULN-AUTHZ-01',   'Staff endpoints require a staff role',                  'code', false),
  ('VULN-OAUTH-01',   'OAuth state/CSRF protection on social login',           'code', true),
  ('VULN-OAUTH-02',   'Strict OAuth redirect-URI matching',                    'infra', true),
  ('VULN-OAUTH-03',   'Link social logins only on a verified email',           'code', false),
  ('VULN-PWD-01',     'Password storage',                                      'data', false),
  ('VULN-PCI-01',     'Card data storage (tokenisation)',                      'data', false),
  ('VULN-GUARD-01',   'DB guard: order quantity must be positive',             'data', false),
  ('VULN-GUARD-02',   'DB guard: unit price must not be negative',             'data', false),
  ('VULN-GUARD-03',   'DB guard: invoice amount must not be negative',         'data', false),
  ('VULN-GUARD-04',   'DB guard: payment amount must be positive',             'data', false),
  ('VULN-GUARD-05',   'DB guard: no cancelling after shipping',                'data', false),
  ('VULN-LLM-01',     'Agent identity from session, not conversation',         'code', false),
  ('VULN-LLM-02',     'Tool results treated as data, minimised',               'code', false),
  ('VULN-LLM-05',     'Agent replies encoded before rendering',                'code', false),
  ('VULN-LLM-06',     'Agent asks the customer before state-changing actions', 'code', false),
  ('VULN-MCP-01',     'Tool server calls APIs with the customer''s token',     'code', false),
  ('VULN-WEBHOOK-01', 'Signed payment webhooks',                               'code', false),
  ('VULN-LOG-01',     'Audit logging of security events',                      'code', false),
  ('VULN-HEADERS-01', 'Security response headers (CSP) on store-web',          'code', false),
  ('VULN-GRAFANA-01', 'Grafana requires login (no anonymous Admin)',           'infra', true),
  ('VULN-WAF-01',     'Edge WAF blocks naive attack signatures',               'infra', true);

INSERT INTO presets (id, name, description) VALUES
  (1, 'All vulnerable', 'Every defence off: the range as attackers first find it'),
  (2, 'All secure',     'Every defence on: the hardened store');

INSERT INTO preset_flags (preset_id, vuln_id, mode)
SELECT 1, vuln_id, 'vulnerable' FROM defence_flags
UNION ALL
SELECT 2, vuln_id, 'secure' FROM defence_flags;

SELECT setval(pg_get_serial_sequence('presets', 'id'), (SELECT max(id) FROM presets));

RESET ROLE;
