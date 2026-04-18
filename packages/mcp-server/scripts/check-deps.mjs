#!/usr/bin/env node
/**
 * Story 8.6 — Dependency allowlist check
 *
 * Verifies that every dependency in package.json is on the allowlist.
 * Prevents accidental or malicious dependency additions from going unnoticed.
 *
 * Usage: node scripts/check-deps.mjs
 */

import { readFileSync } from "node:fs";
import { resolve, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = dirname(fileURLToPath(import.meta.url));
const pkgPath = resolve(__dirname, "..", "package.json");

// --- Allowlist -----------------------------------------------------------
// Update this list when intentionally adding or removing a dependency.

const ALLOWED_DEPS = new Set([
  "@modelcontextprotocol/sdk",
]);

const ALLOWED_DEV_DEPS = new Set([
  "@types/node",
  "typescript",
  "vitest",
]);

// --- Check ---------------------------------------------------------------

const pkg = JSON.parse(readFileSync(pkgPath, "utf-8"));

let failed = false;

function check(label, actual, allowed) {
  const names = Object.keys(actual ?? {});
  for (const name of names) {
    if (!allowed.has(name)) {
      console.error(`UNEXPECTED ${label}: ${name}`);
      failed = true;
    }
  }
  for (const name of allowed) {
    if (!actual?.[name]) {
      console.warn(`MISSING ${label} (on allowlist but not in package.json): ${name}`);
    }
  }
}

check("dependency", pkg.dependencies, ALLOWED_DEPS);
check("devDependency", pkg.devDependencies, ALLOWED_DEV_DEPS);

if (failed) {
  console.error(
    "\nDependency allowlist check failed.\n" +
    "If the new dependency is intentional, add it to scripts/check-deps.mjs."
  );
  process.exit(1);
} else {
  console.log("Dependency allowlist check passed.");
}
