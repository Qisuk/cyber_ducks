# ADR-0001: Blazor Web App front end + separate ASP.NET Core Web API

- **Status:** Accepted
- **Date:** 2026-10-10

## Context
The store needs a C# Blazor front end. The range must expose realistic attack surface,
especially API-level broken access control (IDOR, mass assignment, business-logic bypass).

## Options
1. **Blazor Server only.** UI and data access in one process; traffic goes over a SignalR circuit.
2. **Blazor Web App + separate Web API.** UI calls a REST API over HTTP.
3. **Blazor WebAssembly + Web API.** All UI logic runs in the browser.

## Decision
Option 2, targeting .NET 10 (LTS).

## Consequences
- There is a clear trust boundary (web ↔ API) for the threat model.
- A real HTTP API can be attacked with Burp/curl, so "the UI hid the button but the API allowed it" can be demonstrated.
- There's one more service to build, plus a token flow between web and API.
