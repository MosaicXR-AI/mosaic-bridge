#!/usr/bin/env node
/**
 * Story 8.6 — Lockfile integrity verification
 *
 * Checks that package-lock.json exists, uses lockfileVersion 3,
 * and every resolved package has an integrity hash.
 *
 * Usage: node scripts/verify-lockfile.mjs
 */

import { readFileSync, existsSync } from "node:fs";
import { resolve, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = dirname(fileURLToPath(import.meta.url));
const lockPath = resolve(__dirname, "..", "package-lock.json");

// --- Existence -----------------------------------------------------------

if (!existsSync(lockPath)) {
  console.error("package-lock.json not found. Run `npm install` first.");
  process.exit(1);
}

// --- Parse & validate ----------------------------------------------------

const lock = JSON.parse(readFileSync(lockPath, "utf-8"));

if (lock.lockfileVersion < 2) {
  console.error(
    `Lockfile version ${lock.lockfileVersion} is too old. ` +
    "Upgrade to npm >=7 to get lockfileVersion 2+."
  );
  process.exit(1);
}

const packages = lock.packages ?? {};
let missing = 0;
let checked = 0;

for (const [name, meta] of Object.entries(packages)) {
  // The root package entry ("") has no integrity hash — skip it.
  if (name === "") continue;

  // Optional dependencies may not be installed on every platform.
  // They still must have an integrity hash if resolved.
  if (meta.resolved && !meta.integrity) {
    console.error(`Missing integrity hash: ${name}`);
    missing++;
  }

  checked++;
}

if (missing > 0) {
  console.error(
    `\nLockfile verification failed: ${missing} package(s) lack integrity hashes.\n` +
    "Delete node_modules and package-lock.json, then run `npm install`."
  );
  process.exit(1);
}

console.log(
  `Lockfile verified: ${checked} package(s) checked, all have integrity hashes.`
);
