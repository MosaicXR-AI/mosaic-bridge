#!/bin/bash
# Story 11.3: Run tests with coverage report
set -euo pipefail

cd "$(dirname "$0")/.."
npx vitest run --coverage
