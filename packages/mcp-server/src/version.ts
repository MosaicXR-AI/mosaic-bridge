import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

/**
 * Reads the real package version from package.json at runtime.
 * `../../package.json` resolves to the package root from both `src/version.ts`
 * (dev/test) and the compiled `dist/version.js` — each is one level under root.
 */
export function getVersion(): string {
  const pkgPath = path.resolve(fileURLToPath(import.meta.url), '../../package.json');
  return JSON.parse(readFileSync(pkgPath, 'utf8')).version;
}
