# ZFood build front door. Run `make help` (or bare `make`) for the target list.

SHELL := /bin/bash

# Prefer a dotnet already on PATH; otherwise use the project-local SDK that
# `make setup` installs into .dotnet/ (a convenience for contributors who do
# not have the .NET SDK installed system-wide).
SYSTEM_DOTNET := $(shell command -v dotnet 2>/dev/null)
ifeq ($(SYSTEM_DOTNET),)
DOTNET := $(CURDIR)/.dotnet/dotnet
export DOTNET_ROOT := $(CURDIR)/.dotnet
export PATH := $(CURDIR)/.dotnet:$(PATH)
else
DOTNET := $(SYSTEM_DOTNET)
endif
export DOTNET_CLI_TELEMETRY_OPTOUT := 1

CONFIG ?= Release

.DEFAULT_GOAL := help

.PHONY: help setup run test format smoke screenshots icons publish-linux publish-win clean

help: ## List available targets
	@grep -hE '^[a-zA-Z_-]+:.*?## ' $(MAKEFILE_LIST) | awk 'BEGIN {FS = ":.*?## "}; {printf "  \033[1m%-14s\033[0m %s\n", $$1, $$2}'

setup: ## Install a project-local .NET SDK if none is available, then restore packages
	@if ! command -v dotnet >/dev/null 2>&1 && [ ! -x .dotnet/dotnet ]; then \
		echo "No .NET SDK found; installing one into .dotnet/ ..."; \
		curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0 --install-dir .dotnet; \
	fi
	$(DOTNET) restore ZFood.sln

run: ## Build and run the app
	$(DOTNET) run --project src/ZFood.App

test: ## Run the full test suite
	$(DOTNET) test ZFood.sln

format: ## Verify and fix code formatting
	$(DOTNET) format ZFood.sln

smoke: ## Launch the built app under X11, verify startup, screenshot, exit
	scripts/smoke.sh

screenshots: ## Regenerate the README screenshots from the running app
	scripts/screenshots.sh

icons: ## Regenerate PNG and ICO icons from the SVG source
	scripts/icons.sh

publish-linux: ## Produce a self-contained linux-x64 single-file build in dist/linux-x64
	$(DOTNET) publish src/ZFood.App -c Release -r linux-x64 --self-contained \
		-p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
		-p:DebugType=None -p:DebugSymbols=false -o dist/linux-x64

publish-win: ## Produce a self-contained win-x64 single-file build in dist/win-x64
	$(DOTNET) publish src/ZFood.App -c Release -r win-x64 --self-contained \
		-p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
		-p:DebugType=None -p:DebugSymbols=false -o dist/win-x64

clean: ## Remove build outputs
	$(DOTNET) clean ZFood.sln >/dev/null 2>&1 || true
	rm -rf dist src/ZFood.App/bin src/ZFood.App/obj src/ZFood.Core/bin src/ZFood.Core/obj tests/ZFood.Tests/bin tests/ZFood.Tests/obj
