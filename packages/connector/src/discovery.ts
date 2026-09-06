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
  // "then start the connector again" was wrong advice and reached people through the two
  // busiest surfaces — every editor_call and editor_routes failure — while editor_status
  // had already been corrected. The connector reconnects by itself; telling someone to
  // restart it sends them to fix the one part that is working.
  throw new Error(
    "No running Unity Editor with the Mosaic Bridge was found. Open the Unity project on " +
      "that machine and check its Console: the Mosaic package may still be importing, or may " +
      "have failed to compile. Leave the connector running — it reconnects on its own."
  );
}

/** A discovery file proves a Unity Editor once wrote one, not that a bridge is
 *  answering now. The file survives the Editor closing, and it survives an Editor
 *  whose bridge never compiled: an acceptance test found the connector announcing
 *  "Unity 6000.4.8f1 on port 8282" while nothing was listening on that port at all,
 *  and every subsequent call failing with a bare "fetch failed". Probe before
 *  claiming. */
export async function bridgeAlive(d: Discovery, timeoutMs = 2500): Promise<boolean> {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), timeoutMs);
  try {
    // /health needs no signature; reachability is the only question here.
    const res = await fetch(`http://127.0.0.1:${d.port}/health`, { signal: controller.signal });
    return res.ok || res.status === 401;
  } catch {
    return false;
  } finally {
    clearTimeout(timer);
  }
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
      const d = findDiscovery(explicit);
      // A stale file is worse than none: it makes the wait succeed against an Editor
      // that is not there.
      if (await bridgeAlive(d)) return d;
      if (Date.now() - started > timeoutMs) return null;
      onTick?.(Math.round((Date.now() - started) / 1000));
      await new Promise((r) => setTimeout(r, 3000));
      continue;
    } catch {
      if (Date.now() - started > timeoutMs) return null;
      onTick?.(Math.round((Date.now() - started) / 1000));
      await new Promise((r) => setTimeout(r, 3000));
    }
  }
}
