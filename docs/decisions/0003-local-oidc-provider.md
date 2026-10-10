# ADR-0003: Local Keycloak as stand-in for Google / Meta OAuth

- **Status:** Accepted
- **Date:** 2026-10-10

## Context
Customers should sign in with a username/password or with "Google"/"Meta". The range must
demonstrate OAuth/OIDC flaws (missing `state`, loose `redirect_uri`, unverified email account linking).

## Options
1. Real Google and Meta developer apps.
2. A local Keycloak with two identity configurations branded "Google" and "Meta".

## Decision
Option 2. Keep the auth code provider-agnostic (standard OIDC), so real Google could be
added later behind a config switch.

## Consequences
- Fully offline, with no third-party registrations.
- The IdP side can be deliberately misconfigured, and both sides of the flow are observable.
- We must not attack real Google/Meta endpoints. This stays out of scope.
- Keycloak costs about 600 MB of RAM.
