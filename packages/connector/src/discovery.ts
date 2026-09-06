/** Finding the running Editor's bridge.
 *
 *  Shared, because both the connection loop and `setup` need it: setup uses it to
 *  wait until the Editor is actually reachable, so a person is told they are
 *  connected rather than told to hope.
 */
import { readFileSync, existsSync } from "node:fs";
import { join } from "node:path";

export interface Discovery {
  port: number;
  secret_base64: string;
  unity_project_path?: string;
  unity_version?: string;
}

export function sharedBase(): string {
  const home = process.env.HOME ?? process.env.USERPROFILE ?? "";
  if (process.platform === "darwin") return join(home, "Library", "Application Support", "Mosaic", "Bridge");
  if (process.platform === "win32") {
    const local = process.env.LOCALAPPDATA ?? join(home, "AppData", "Local");
    return join(local, "Mosaic", "Bridge");
  }
  const xdg = process.env.XDG_RUNTIME_DIR;
  return xdg && existsSync(xdg) ? join(xdg, "mosaic-bridge") : join(home, ".mosaic", "bridge");
}

/** One Editor is the normal case; when several are running the registry lists them
 *  and we take the most recently started, which is the one a person just opened. */
export function findDiscovery(explicit?: string): Discovery {
  const candidates: string[] = [];
  if (explicit) candidates.push(explicit);
  const base = sharedBase();
  const registry = join(base, "instance-registry.json");
  if (existsSync(registry)) {
    try {
      const reg = JSON.parse(readFileSync(registry, "utf8").replace(/^﻿/, ""));
      // The Editor writes camelCase; older tooling used snake_case. Accept both
      // rather than silently finding nothing.
      const entries: any[] = Array.isArray(reg) ? reg : reg.instances || reg.entries || [];
      const hashOf = (e: any) => e.projectHash || e.project_hash;
      const dirOf = (e: any) => e.runtimeDir || e.runtime_dir;
      const startedOf = (e: any) =>
        e.startedUnixSeconds || e.started_unix_seconds || (e.registeredAt ? Date.parse(e.registeredAt) / 1000 : 0);
      const sorted = entries.filter((e) => e && (dirOf(e) || hashOf(e))).sort((a, b) => startedOf(b) - startedOf(a));
      for (const e of sorted) {
        candidates.push(dirOf(e) ? join(dirOf(e), "bridge-discovery.json") : join(base, hashOf(e), "bridge-discovery.json"));
      }
    } catch {
      /* fall through to the direct guess below */
    }
  }
  candidates.push(join(base, "bridge-discovery.json"));

  for (const c of candidates) {
    if (existsSync(c)) {
      const d = JSON.parse(readFileSync(c, "utf8").replace(/^﻿/, "")) as Discovery;
      if (d.port && d.secret_base64) return d;
    }
  }
  throw new Error(
    "No running Unity Editor with the Mosaic Bridge was found. Open the Unity project first, " +
      "then start the connector again."
  );
}

/** Wait for an Editor to appear, reporting as it goes. Returns null on timeout so the
 *  caller can say something useful rather than throwing at a person. */
export async function waitForEditor(
  timeoutMs: number,
  onTick?: (secondsWaited: number) => void,
  explicit?: string
): Promise<Discovery | null> {
  const started = Date.now();
  for (;;) {
    try {
      return findDiscovery(explicit);
    } catch {
      if (Date.now() - started > timeoutMs) return null;
      onTick?.(Math.round((Date.now() - started) / 1000));
      await new Promise((r) => setTimeout(r, 3000));
    }
  }
}
