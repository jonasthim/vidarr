.PHONY: help dev build test format coverage clean seed check-tools

help: ## Show this help
	@awk 'BEGIN{FS=":.*##"; printf "\nVidarr — common dev targets:\n\n"} \
	      /^[a-zA-Z_-]+:.*##/{printf "  \033[36m%-14s\033[0m %s\n", $$1, $$2}' $(MAKEFILE_LIST)
	@printf '\nSee docs/development.md for the full guide.\n\n'

check-tools: ## Verify dotnet, node, ffmpeg, yt-dlp are on PATH
	@scripts/check-tools.sh

dev: check-tools ## Run backend + Vite together (Ctrl-C stops both)
	@scripts/dev.sh

build: ## dotnet build
	dotnet build

test: ## Full test suite (Release, same as CI)
	dotnet test --configuration Release -p:SkipWebBuild=true

format: ## Format check (parity with CI)
	dotnet format --verify-no-changes

coverage: ## Coverage run + HTML report (opens in browser)
	@scripts/coverage.sh

seed: ## POST sample artists + a root folder to the running dev backend
	@scripts/seed.sh

clean: ## Wipe data/, TestResults/, and all bin/obj
	rm -rf data/ TestResults/
	find . -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} +
