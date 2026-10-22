# Cyber Ducks range — common operations. Run `make help` for the list.
# All data lives in Docker volumes; `up`/`stop` preserve it, `reset` wipes it.

COMPOSE ?= docker compose

.PHONY: help up stop down start rebuild reset ps logs smoke flags

help: ## Show this help
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | sort | awk 'BEGIN{FS=":.*?## "}{printf "  \033[36m%-10s\033[0m %s\n", $$1, $$2}'

up: ## Start the range (build if needed), preserving data
	$(COMPOSE) up -d --build

start: ## Start the range without rebuilding
	$(COMPOSE) up -d

stop: ## Stop the range, keeping containers and data
	$(COMPOSE) stop

down: ## Stop and remove containers (keeps data volumes)
	$(COMPOSE) down

rebuild: ## Rebuild all images and recreate
	$(COMPOSE) build
	$(COMPOSE) up -d

reset: ## DESTROY all data and start fresh (new flag GUIDs, empty scoreboard)
	$(COMPOSE) down -v
	$(COMPOSE) up -d --build

ps: ## Show container status
	$(COMPOSE) ps

logs: ## Tail logs for all services (Ctrl-C to stop)
	$(COMPOSE) logs -f --tail=50

smoke: ## Run the pre-flight smoke test
	@bash scripts/smoke-test.sh

flags: ## Show the CTF flag manifest (facilitator view: slugs, placement, GUIDs)
	@$(COMPOSE) exec -T postgres psql -U duckadmin -d range_control -c \
		"SELECT slug, vuln_id, points, placed, guid FROM ctf_flags ORDER BY points, slug;"
