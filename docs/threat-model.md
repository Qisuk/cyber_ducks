# Threat Model: Rubber Duck Emporium

> Method: Shostack's four questions.
> 1. What are we working on? 2. What can go wrong? 3. What are we going to do about it?
> 4. Did we do a good job?
>
> Frameworks: **STRIDE** per element, **OWASP Top 10** (web), **OWASP Top 10 for LLM Apps** (agent).
>
> Note: for this range, "what are we going to do about it" usually means *"intentionally leave it
> vulnerable, then detect it, then fix it"*. Record all three.

---

## 1. What are we working on?

### 1.1 Scope
In scope: every container in `docs/architecture.md`.
Out of scope: the Docker host, real Google/Meta, the internet.

### 1.2 Assets
*What would an attacker want, or what would hurt if it were lost? Rate C/I/A importance (H/M/L).*

| ID | Asset | C | I | A | Notes |
|----|-------|---|---|---|-------|
| A1 | Customer PII (name, email, address) | H | M | L | |
| A2 | PCI data (card number, CVV, expiry) | H | M | L | Stored in the DB when `Vulns__StoreCardData=true` (ADR-0007) |
| A3 | API keys | H | H | L | Keys for communication between services |
| A4 | User/admin passwords | H | H | L | Hashed or unhashed creds |
| A5 | DB credentials | H | H | L | DB creds, if stored anywhere |
| A6 | Non-PII customer data (order history, payment history, etc.) | H | L | L | Info that may not be sensitive but should not be shared |
| A7 | System chat prompt | M | H | L | |
| A8 | Chat context/history | H | M | L | |
| A9 | Infrastructure info (architecture details) | M | L | L | |
| A10 | Logs/telemetry | H | H | H | |
| A11 | Customer addresses | H | L | L | |
| A12 | Session tokens | H | H | L | |
| A13 | Customer orders | M | H | M | Ability to modify/create/remove customer orders |
| A14 | Customer payments | M | H | M | Ability to modify payments, payment history, or payment status |
| A15 | Database backups | H | H | H | |
| A16 | Business reputation & regulatory standing | L | H | L | |
| A17 | Staff privileges / role assignments | M | H | M | Who is a customer, employee, admin or auditor. Target of elevation-of-privilege threats. |

*Prompts: order data, invoices and payment status, credentials/password hashes, session tokens/JWT
signing key, staff privileges, the LLM system prompt, the LLM compute budget, logs themselves…*

### 1.3 Actors
| ID | Actor | Trust level | Goals |
|----|-------|-------------|-------|
| T1 | Anonymous visitor | Untrusted | Browse ducks; *or* probe for weaknesses |
| T2 | Logged-in shopper | Semi-trusted | Use the shop as intended *or* probe and exploit weaknesses |
| T3 | Administrator | Trusted | Manage users, roles and store config *or* grant themselves or others excess privileges, disable logging, destroy data |
| T4 | Auditor | Trusted | Access and review everything *(open: read-only? build an auditor role? Decide in Phase 3)* |
| T5 | Employee | Trusted | View and ship all orders, handle cancellations *or* browse customer data and cards beyond need, tamper with orders |
| T6 | Supply-chain attacker | Untrusted | Attack through external dependencies (LLM, tooling, APIs, imported packages) |
| T7 | Indirect prompt injector | Any | Malicious user who inserts prompt injections in order notes/reviews etc. to trigger malicious agent behaviour |
| T8 | AI agent | Untrusted | Could try to do anything |
| T9 | Compromised account | Appears trusted | Escalate/elevate/pivot to cause damage, extract data |
| T10 | External systems | Semi-trusted | May be compromised in a supply-chain attack |
| T11 | External bots | Untrusted | May hit the API/LLM and run up charges or cause unexpected behaviour |
| T12 | Unknown adversary (wargame) | Untrusted | Motive set secretly by the facilitator per scenario (financial, ransomware/extortion, hacktivist, competitor); players must infer it from evidence |
| T13 | Facilitator (wargame) | Trusted, out of scope | Runs the exercise; sees ground truth (ADR-0008) |

*Prompts: authenticated customer (honest vs malicious), staff, a compromised IdP account,
an attacker who plants content the AI later reads, an insider with Grafana access…*

### 1.4 Data-flow diagram & trust boundaries
Flows F1–F20 are numbered in the diagram and flow table in `architecture.md`.

**Method:** group components into **trust zones** (everything in a zone trusts everything else in it
equally). A **trust boundary** is the line between two zones. Many flows can cross one boundary.
Every boundary is a place where the receiving side must *check* what arrives (authenticate,
authorise, validate, sanitise), so for each boundary we record which direction is dangerous and
what the receiver must check.

#### Trust zones
| Zone | Contains | Notes |
|------|----------|-------|
| Z1 Untrusted clients | Customer & staff browsers, bots | Anything from here can be forged. Staff get trust from *authentication*, not from the network. |
| Z2 Edge | proxy | Decide: is this really a separate zone from Z3? |
| Z3 Application | store-web, store-api | Contains two sub-zones: |
| ↳ Z3a Agent (untrusted) | ai-agent | Untrusted actor (T8). Its edge is TB5. |
| ↳ Z3b Tool gateway | agent-tools | Policy enforcement point for every tool call. Its edge towards the APIs is TB11 (ADR-0009). |
| Z4 LLM | ollama | Separate zone because its *output* is untrusted |
| Z5 Data | postgres | |
| Z6 Backups | backups | Kept separate from Z5 so one compromise can't reach both (ransomware) |
| Z7 External providers | idp, payment-stub, mailpit | Stand-ins for third parties outside our control |
| Z8 Observability | otel-collector / LGTM | |
| Z9 Range control | ops-console | Most trusted; out of attack scope (ADR-0008) |

#### Trust boundaries
| ID | Boundary | Crossed by flows | Dangerous direction | Receiver must check |
|----|----------|------------------|---------------------|---------------------|
| TB1 | Z1 Untrusted clients → Z2 Edge | F1, F2 | Inbound (Z1 → Z2). Every byte is attacker-controllable: body, headers, cookies, hidden fields, client-side "validation". | Valid session/token on every request; server-side validation of all input; rate limiting; never rely on UI-only checks. *(No TLS locally, so traffic can be sniffed; note as accepted risk.)* |
| TB2 | Z2 Edge → Z3 Application | F3 | Inbound. The proxy forwards Z1 data unchanged, so it adds no trust today. | Treat everything as Z1 input. Only trust `X-Forwarded-*` headers when they come from the proxy (otherwise IP spoofing defeats logging and rate limits). *Decision: keep Z2 separate as the future WAF/TLS-termination point (Phase 8).* |
| TB3 | Z1 Untrusted clients ↔ Z7 External providers | F11 | Both. Z1 → IdP carries credentials (brute force, credential stuffing). IdP → Z1 redirect carries the auth code (stolen via a loose `redirect_uri`). | IdP: exact `redirect_uri` match, lockout/rate limiting. App: `state`, `nonce` and PKCE on every login. |
| TB4 | Z3 Application ↔ Z4 LLM | F6, F7 | **F7 (model → agent)** for integrity: output may carry injected instructions or bogus tool calls. F6 for confidentiality: everything in the prompt is exposed to the model. | Treat model output as untrusted input. Validate tool-call names and arguments against a schema. Only put data into the prompt that the current user may see. Limit tokens and timeouts (LLM10). |
| TB5 | ai-agent ↔ rest of Z3 (store-web, agent-tools) | F5, F8, F9, F21 | **F8**: tool calls driven by possibly injected model output. **F21**: tool results carry other people's text into the agent's context (indirect injection, T7). **F9**: model text rendered in the customer's browser. | agent-tools: allow-listed tools, schema-validated arguments, user confirmation for state-changing tools. Results minimised, size-limited, marked as data not instructions. store-web HTML-encodes replies. F5 is rate/length limited. (ADR-0009) |
| TB6 | Z3 Application → Z5 Data | F10 | Inbound to DB: queries built from user input. | Parameterised queries only. Least-privilege DB role (not superuser, no access to card data it doesn't need). Separate roles per service. |
| TB7 | Z5 Data → Z6 Backups | F17 (and F20 restores) | Both. Z5 compromise → backups deleted or encrypted (ransomware). Restore direction → a tampered backup is put back. | Backups append-only from Z5 with separate credentials. Integrity hashes. Regular test restores. |
| TB8 | Z3 Application ↔ Z7 External providers | F12, F13, F14, F15 | **F14 webhook (inbound)**: anyone who reaches it can claim "invoice paid". F12: forged/unsigned tokens. F13/F15 outbound: card data and reset links leave our control. | Webhook: HMAC signature, timestamp/replay check, re-verify payment with the provider. Tokens: verify signature, issuer, audience, expiry, pinned `alg`. Reset links built from config, never from the `Host` header. |
| TB9 | Z3 Application → Z8 Observability | F16 | Inbound to telemetry: attacker-controlled text (usernames, chat messages) lands in logs. Sensitive data can leak *into* logs. | Structured logging (no string concatenation, so no forged log lines). Redact passwords, tokens and card numbers before export. Grafana escapes rendered values. The collector only accepts known services. |
| TB11 | agent-tools ↔ any API (store-api now; external APIs later) | F22, F23 | Both. **F22**: the tool server could act with more privilege than the customer. **F23**: API responses may contain injected text or more data than the tool needs. | Call APIs with the customer's own token, so store-api authorises as that user. Never use a service account. Validate and filter responses before they reach F21. Allow-list destination hosts (no arbitrary URL fetch, so no SSRF). |
| TB10 | Z9 Range control → all zones | F18, F19, F20 | **F20 outbound** is the most powerful flow in the system: control of containers usually means Docker socket access, which equals root on the host. F18 inbound: players must not reach facilitator powers. | Strong auth and role checks on the console. Ops network unreachable from Z1–Z8. Response actions are allow-listed scripts, never arbitrary commands. The Docker socket is never mounted into any range service. |

**Flow coverage:** F1, F2 → TB1 · F3 → TB2 · F5, F8, F9, F21 → TB5 · F6, F7 → TB4 · F10 → TB6 ·
F11 → TB3 · F12–F15 → TB8 · F16 → TB9 · F17 → TB7 · F18–F20 → TB10 · F22, F23 → TB11.
**F4 (store-web → store-api)** crosses no zone boundary, but the data in it originated in Z1.
store-api must still authorise every request per user and never assume "the web app already checked"
(ADR-0001's point about UI-only checks).

---

## 2. What can go wrong?

### 2.1 STRIDE per boundary

**Method:** for each trust boundary (TB1–TB11), take the flows that cross it and ask all six STRIDE
questions. Each "receiver must check" item in §1.4 hints at a threat: the threat is what happens
when that check is missing. Not every letter applies to every boundary; write "n/a" in the
tracker rather than leaving it blank, so it's clear you considered it.

**Level of detail:** describe *what can go wrong*, not *how to exploit it*. Name the weakness
class (IDOR, mass assignment, replay…), not endpoints, field names or payloads. Those don't exist
yet, and they belong in §3 (vuln IDs) and `attacks/<VULN-ID>/`. Use this template:

> **[Actor]** can **[action]** by exploiting **[weakness class]** at **[boundary]**, resulting in **[impact on asset]**.

#### STRIDE cheat sheet
| Letter | Property violated | Question to ask at the boundary | Example in this system |
|--------|-------------------|---------------------------------|------------------------|
| **S**poofing | Authentication | Can someone pretend to be another user or service? | Forged webhook claiming to be payment-stub |
| **T**ampering | Integrity | Can data be changed in transit or before it's trusted? | Server trusts a client-supplied price |
| **R**epudiation | Non-repudiation | Could someone deny an action, and could we prove otherwise? | "I never cancelled that order", with no audit log |
| **I**nformation disclosure | Confidentiality | Can data reach someone who shouldn't see it? | Agent returns another customer's order |
| **D**enial of service | Availability | Can someone exhaust or break it? | Long prompts pinning the CPU-only LLM |
| **E**levation of privilege | Authorisation | Can someone gain powers they shouldn't have? | Shopper becomes staff via mass assignment |

#### Rating scales
- **Likelihood:** how likely in a *real, unhardened production store* (everything is likely on the range by design).
  **H** = trivial with a browser/proxy tool; **M** = needs skill or a precondition; **L** = needs insider access or a rare condition.
- **Impact:** take the highest C/I/A rating (§1.2) of the asset affected, for the property violated.

#### Coverage tracker
| Boundary | S | T | R | I | D | E |
|----------|---|---|---|---|---|---|
| TB1 Untrusted clients → Edge | TH1 | TH2 | TH3 | TH4, TH5 | TH6 | TH7 |
| TB2 Edge → Application | | | | | | |
| TB3 Clients ↔ External providers | | | | | | |
| TB4 Application ↔ LLM | | TH10 | | | | |
| TB5 ai-agent ↔ rest of Z3 | TH11 | | TH12 | TH8 | | TH9 |
| TB6 Application → Data | | | | | | |
| TB7 Data → Backups | | | | | | |
| TB8 Application ↔ External providers | | | | | | |
| TB9 Application → Observability | | | | | | TH13 |
| TB10 Range control → all zones | | | | | | |
| TB11 agent-tools ↔ any API | | | | | | |

#### Threats
| ID | Boundary / flow | STRIDE | Threat | Actor | Asset | L | I |
| --- | --- | --- | --- | --- | --- | --- | --- |
| TH1 | TB1 / F1, F2 | S | An attacker can act as another customer or staff member by exploiting weak session or token handling (theft, replay, forgery), resulting in unauthorised actions under the victim's identity | T9 | A12 | M | H |
| TH2 | TB1 / F1 | T | A shopper can alter business values (price, quantity, amount due) by exploiting server-side trust in client-supplied data, resulting in fraudulent orders or payments | T2 | A13, A14 | H | H |
| TH3 | TB1 / F1 | R | A customer can deny performing an order action by exploiting insufficient audit logging, resulting in disputes the business cannot resolve | T2 | A13, A10 | M | M |
| TH4 | TB1 / F1 → F4 | I | A shopper can read other customers' orders by exploiting missing object-level authorisation (IDOR), resulting in disclosure of customer data | T2 | A1, A6 | H | H |
| TH5 | TB1 / F1 | I | An anonymous visitor can learn internal details of the system by exploiting verbose error handling, resulting in easier reconnaissance | T1 | A9 | H | M |
| TH6 | TB1 / F1 | D | Automated bots can make the store unavailable by exploiting the absence of rate limiting, resulting in lost sales and reputation | T11 | A16 | M | H |
| TH7 | TB1 / F1 → F4 | E | A shopper can gain staff powers by exploiting unrestricted binding of client-supplied fields (mass assignment), resulting in privilege escalation | T2 | A17 | H | H |
| TH8 | TB5 / F21 | I | An indirect prompt injector can make the agent disclose other customers' data by exploiting the agent treating tool results as instructions (indirect prompt injection), resulting in disclosure of customer data | T7 | A1, A6 | H | H |
| TH9 | TB5 / F21 → F8 | E | An indirect prompt injector can make the agent perform actions beyond the current user's rights by exploiting indirect prompt injection combined with over-privileged tools (excessive agency), resulting in unauthorised changes to orders or payments | T7 | A13, A14 | M | H |
| TH10 | TB4 / F7 | T | A supply-chain attacker can make the model emit harmful tool calls or misleading answers by exploiting unverified model provenance (poisoned or backdoored model), resulting in corrupted agent behaviour | T6 | A8, A13 | L | H |
| TH11 | TB5 / F5 | S | A shopper can make the agent act on another customer's account by exploiting the agent trusting identity claimed in conversation instead of the authenticated session (direct prompt injection), resulting in actions and disclosures under the victim's identity | T2 | A1, A6, A13 | H | H |
| TH12 | TB5 / F8 | R | A shopper can deny instructing the agent to perform an action by exploiting the lack of an audit trail linking agent actions to the prompting user and prompt, resulting in disputes that cannot be resolved | T2 | A13, A10 | M | M |
| TH13 | TB9 / F16 | E | An attacker can run code in an analyst's browser by exploiting unsanitised attacker-controlled text in telemetry (log injection / stored XSS in the log viewer), resulting in compromise of the analyst's session and trust in the logs | T7 | A10, A12 | M | H |
| TH14 | | | | | | | |
| TH15 | | | | | | | |
| TH16 | | | | | | | |
| TH17 | | | | | | | |
| TH18 | | | | | | | |
| TH19 | | | | | | | |
| TH20 | | | | | | | |
| TH21 | | | | | | | |

### 2.2 AI agent threats (OWASP LLM Top 10)
| ID | LLM category | Threat | Path |
|----|--------------|--------|------|
| AI1 | LLM01 Prompt injection (indirect) | An attacker can control the agent's behaviour by planting instructions in content it later reads as tool results, resulting in actions or disclosures on another customer's behalf | T7 → stored content → F23 → F21 → F6 |
| AI2 | | | |

---

## 3. What are we going to do about it?
| Threat | Range decision | Vuln ID | Detection idea | Fix (hardened mode) |
|--------|----------------|---------|----------------|---------------------|
| | | | | |

Range decision is one of: **Leave vulnerable** (target), **Mitigate** (out of exercise scope),
**Accept**.

---

## 4. Did we do a good job?
To review at the end of Phase 7/8:
- [ ] Every vulnerability ID traces back to a threat here
- [ ] Every threat has a detection idea
- [ ] Any surprises found during attacks are added back here
