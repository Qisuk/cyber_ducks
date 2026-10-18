# Vulnerability catalogue

Every intentional weakness in the range has an ID and a defence flag (ADR-0006, ADR-0011). Flip a
flag in **Range Control** (`http://localhost:8090`, Defences tab) to switch between the vulnerable
and secure code path; most apply within seconds, a few need a restart (marked below).

- **Attack / confirm + fix** per ID: [`attacks/<ID>/`](../attacks)
- **Detection** per ID: [`detections/<ID>.md`](../detections)
- The flag titles below describe the **secure** behaviour (what "secure" turns on).

**Catalogued so far:** the application/API set (IDOR, SQLI, AUTHZ, MASS, LOGIC-01/02, AUTH, INFO),
the browser tier (XSS, CSRF, SESSION), WEBHOOK, the DB-guard family, and LLM-06 — each with a README
+ confirm probe (or deterministic test) + detection. Also catalogued: OAUTH-01/02/03, PWD, PCI,
DBROLE. Pending: LLM-01/02/05, MCP-01, LOG-01.

## Application & API

| ID | Secure behaviour | Restart | OWASP | Catalogued |
|----|------------------|:---:|------|:---:|
| [VULN-IDOR-01](../attacks/VULN-IDOR-01/) | Object-level authorisation on orders and invoices | | API1 BOLA | ✅ |
| [VULN-SQLI-01](../attacks/VULN-SQLI-01/) | Parameterised queries | | A03 Injection | ✅ |
| [VULN-AUTHZ-01](../attacks/VULN-AUTHZ-01/) | Staff endpoints require a staff role | | API5 BFLA | ✅ |
| [VULN-MASS-01](../attacks/VULN-MASS-01/) | Allow-listed fields on profile updates | | API6 / Mass assignment | ✅ |
| [VULN-LOGIC-01](../attacks/VULN-LOGIC-01/) | Server recomputes prices and totals | | Business logic | ✅ |
| [VULN-LOGIC-02](../attacks/VULN-LOGIC-02/) | Order state rules enforced server-side | | Business logic | ✅ |
| [VULN-XSS-01](../attacks/VULN-XSS-01/) | Output encoding of user content | | A03 XSS | ✅ |
| [VULN-CSRF-01](../attacks/VULN-CSRF-01/) | Anti-forgery tokens | | A01 / CSRF | ✅ |
| [VULN-AUTH-01](../attacks/VULN-AUTH-01/) | Login throttling and lockout | | A07 | ✅ |
| [VULN-SESSION-01](../attacks/VULN-SESSION-01/) | Session and token handling | | A07 | ✅ |
| [VULN-INFO-01](../attacks/VULN-INFO-01/) | Generic error responses | | A05 | ✅ |
| [VULN-DBROLE-01](../attacks/VULN-DBROLE-01/) | store-api connects as least-privilege role | restart | A05 | ✅ |

## Auth & OAuth

| ID | Secure behaviour | Restart | OWASP | Catalogued |
|----|------------------|:---:|------|:---:|
| [VULN-OAUTH-01](../attacks/VULN-OAUTH-01/) | OAuth state/CSRF protection on social login | restart | A07 | ✅ |
| [VULN-OAUTH-02](../attacks/VULN-OAUTH-02/) | Strict OAuth redirect-URI matching | restart | A07 | ✅ |
| [VULN-OAUTH-03](../attacks/VULN-OAUTH-03/) | Link social logins only on a verified email | | A07 | ✅ |
| [VULN-PWD-01](../attacks/VULN-PWD-01/) | Password storage | | A02 | ✅ |
| [VULN-PCI-01](../attacks/VULN-PCI-01/) | Card data storage (tokenisation) | | A02 / PCI | ✅ |

## AI agent (OWASP LLM Top 10)

| ID | Secure behaviour | Restart | OWASP | Catalogued |
|----|------------------|:---:|------|:---:|
| VULN-LLM-01 | Agent identity from session, not conversation | | LLM01 / LLM08 | wired: no |
| VULN-LLM-02 | Tool results treated as data, minimised | | LLM01 / LLM02 | wired ✓ |
| VULN-LLM-05 | Agent replies encoded before rendering | | LLM05 | wired ✓ |
| [VULN-LLM-06](../attacks/VULN-LLM-06/) | Agent asks the customer before state-changing actions | | LLM06 Excessive agency | ✅ |
| VULN-MCP-01 | Tool server calls APIs with the customer's token | | LLM06 / LLM08 | wired: no |

## Database guards (defence in depth)

Toggleable Postgres constraints/triggers (`set_guard()`), a second layer under the app checks.

Documented as a family in [attacks/VULN-GUARD-01/](../attacks/VULN-GUARD-01/) (GUARD-01 has a live
probe; the others are shown via their partner flaws). ✅

| ID | Secure behaviour | Shown by |
|----|------------------|----------|
| VULN-GUARD-01 | Order quantity must be positive | [probe](../attacks/VULN-GUARD-01/) |
| VULN-GUARD-02 | Unit price must not be negative | VULN-LOGIC-01 vulnerable |
| VULN-GUARD-03 | Invoice amount must not be negative | negative-priced order |
| VULN-GUARD-04 | Payment amount must be positive | payment path |
| VULN-GUARD-05 | No cancelling after shipping | [VULN-LOGIC-02](../attacks/VULN-LOGIC-02/) |

## Infrastructure & observability

| ID | Secure behaviour | OWASP |
|----|------------------|------|
| [VULN-WEBHOOK-01](../attacks/VULN-WEBHOOK-01/) | Signed payment webhooks ✅ | A08 |
| VULN-LOG-01 | Audit logging of security events | A09 |
