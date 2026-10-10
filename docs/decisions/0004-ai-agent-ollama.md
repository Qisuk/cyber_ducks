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
| `qwen3:4b`   | ~3 GB  | OK           | Good         | **Default (chosen)** |
| `llama3.2:3b`| ~2.5 GB| Fast         | Fair         | Lightest fallback |
| `qwen3:8b`   | ~5.5 GB| Slow         | Very good    | Tight in a 10 GB budget |

## Consequences
- Small models are less predictable. Prompt-injection demos may need several attempts, which is realistic.
- First start downloads a multi-GB model.
- Responses take seconds on CPU, so the chat UI needs a "thinking…" state and streaming.
