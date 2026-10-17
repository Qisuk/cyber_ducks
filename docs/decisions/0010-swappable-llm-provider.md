# ADR-0010: Swappable LLM provider (local by default, cloud optional)

- **Status:** Accepted
- **Date:** 2026-10-17
- **Amends:** ADR-0004

## Context
The agent must run fully offline by default (Ollama), but it should also be possible to compare
behaviour with a cloud model. Smaller local models are less predictable, and prompt-injection
demos behave differently on stronger models. Switching must not require code changes.

## Decision
- ai-agent talks only to `Microsoft.Extensions.AI.IChatClient`. A factory picks the implementation
  from configuration at startup:

  | `Llm__Provider` | Backend | Notes |
  |-----------------|---------|-------|
  | `Ollama` (**default**) | local `ollama` container | `Llm__Model` default `qwen3:4b` |
  | `Anthropic` | Claude API | e.g. `claude-haiku-5-5` (cheap) or `claude-sonnet-5-5` |
  | `OpenAICompatible` | Any OpenAI-compatible endpoint (OpenAI, Azure OpenAI, LM Studio, vLLM) | `Llm__Endpoint` required |

- Settings: `Llm__Provider`, `Llm__Model`, `Llm__Endpoint`, `Llm__ApiKey`, plus token/timeout limits.
  Exact NuGet packages per provider are chosen in Phase 6.
- The `ollama` service sits in a Compose profile (`local-llm`, enabled by default in `.env`), so
  cloud runs can skip it and save ~3–4 GB of RAM.
- The provider and model in use are logged at startup and tagged on every agent trace, so
  telemetry shows which model produced each answer.

## Consequences
- **Data leaves the machine with a cloud provider.** Prompts include customer data (fake, but
  still: threat TH20). Cloud mode is opt-in and logged.
- **Real API keys are real secrets.** They go only in the git-ignored `.env`, never in the repo,
  config files or logs (asset A3). Use a dedicated key with a spend limit: unbounded consumption
  (LLM10) costs real money in cloud mode.
- Tool calling and the agent's prompts must work across providers. Test each provider before demos.
- Provider choice becomes a range variable: the same attack can be compared across models.
