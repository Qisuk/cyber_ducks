# ADR-0004: AI agent in C# using a local Ollama container

- **Status:** Accepted
- **Date:** 2026-10-10

## Context
Customers can ask an AI agent about their orders. It must run locally by default and be
usable to demonstrate OWASP LLM Top 10 issues. The host has 8 CPUs, ~9.7 GB of Docker RAM and
**no NVIDIA GPU**, so inference runs on the CPU.

## Decision
- `ollama` container is the default LLM provider. The model is pulled on first start into a named volume.
- The agent is a separate C# service using `Microsoft.Extensions.AI` (`IChatClient`), so the
  provider can be swapped (e.g. a cloud model) via configuration.
- The agent uses **tools** (`get_my_orders`, `get_order_status`, …) that call `store-api`,
  not raw SQL. Vulnerable variants come from poor tool scoping and excessive agency.

## Model options (CPU-only, must support tool calling)
| Model        | RAM    | Speed on CPU | Tool calling | Notes |
|--------------|--------|--------------|--------------|-------|
| `qwen3:4b-instruct` | ~3 GB | OK (~12 tok/s at 4 threads) | Good | **Default (chosen)** — Qwen3-4B-Instruct-2507, no reasoning mode |
| `qwen3:4b`   | ~3 GB  | Very slow    | Good         | Now resolves to the *Thinking-2507* variant: always reasons, minutes per turn on CPU |
| `llama3.2:3b`| ~2.5 GB| Fast         | Fair         | Lightest fallback |
| `qwen3:8b`   | ~5.5 GB| Slow         | Very good    | Tight in a 10 GB budget |

## Consequences
- Small models are less predictable. Prompt-injection demos may need several attempts, which is realistic.
- First start downloads a multi-GB model.
- Responses take seconds on CPU, so the chat UI needs a "thinking…" state and streaming.

## Update (Phase 6b, 2026-10-18)
- The `qwen3:4b` tag now points to the always-thinking 2507 model; a single order question timed
  out after 5 minutes. Default switched to `qwen3:4b-instruct` (same size and family).
- llama.cpp starts one thread per host core; with the container capped at `cpus: 4` the CFS quota
  throttled it to ~3 tok/s. `Llm__Threads` (= the cpus limit) restores ~12 tok/s.
- `OLLAMA_KEEP_ALIVE=24h`: a cold model load takes ~1.5–2 min here, so keep it resident.
  Measured: first question ~200 s (cold), then ~25 s per question with one tool call.
