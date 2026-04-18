#!/usr/bin/env bash
# Story 8.6 — npm supply chain interim mitigations
# Run npm audit and fail on high/critical vulnerabilities
set -euo pipefail

echo "Running npm audit (audit-level=high)..."
npm audit --audit-level=high

echo "Audit passed — no high or critical vulnerabilities found."
