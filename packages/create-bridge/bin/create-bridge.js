#!/usr/bin/env node
import { run } from '../src/cli.js';

run(process.argv).catch((err) => {
  // Top-level safety net. Normal errors are handled inside run() with friendly
  // @clack output; this only fires for truly unexpected exceptions (stack
  // traces in dev, etc.).
  process.stderr.write(`\nUnexpected error: ${err?.stack || err?.message || err}\n`);
  process.exit(1);
});
