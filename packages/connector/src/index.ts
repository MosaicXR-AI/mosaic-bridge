#!/usr/bin/env node
/**
 * mosaic-connector — links this machine's Unity Editor to a Mosaic Cloud service.
 *
 * The connection is made outward only: this process dials the service, so nothing
 * needs to be opened on the customer's network, and the Editor is never exposed to
 * anything except calls arriving on this authenticated socket.
 *
 *   mosaic-connector --url wss://cloud.example.com/tunnel --token <token>
 *
 * Discovery of the local bridge (port and signing secret) uses the same
 * bridge-discovery.json the MCP server reads, so a running Editor is found the same
 * way it always is.
 */
import WebSocket from "ws";
import { readFileSync, existsSync } from "node:fs";
import { join } from "node:path";
import { createHmac, randomUUID } from "node:crypto";

interface Discovery {
  port: number;
  secret_base64: string;
  unity_project_path?: string;
  unity_version?: string;
}

interface Args {
  url: string;
  token: string;
  discoveryFile?: string;
  verbose: boolean;
}

function parseArgs(argv: string[]): Args {
  const get = (flag: string): string | undefined => {
    const i = argv.indexOf(flag);
    return i >= 0 ? argv[i + 1] : undefined;
  };
  const url = get("--url") || process.env.MOSAIC_CLOUD_URL || "";
  const token = get("--token") || process.env.MOSAIC_CLOUD_TOKEN || "";
  if (!url || !token) {
    process.stderr.write(
      "usage: mosaic-connector --url wss://<service>/tunnel --token <token>\n" +
        "       (or set MOSAIC_CLOUD_URL and MOSAIC_CLOUD_TOKEN)\n"
    );
    process.exit(2);
  }
  return { url, token, discoveryFile: get("--discovery-file"), verbose: argv.includes("--verbose") };
}

function sharedBase(): string {
  const home = process.env.HOME ?? process.env.USERPROFILE ?? "";
  if (process.platform === "darwin") return join(home, "Library", "Application Support", "Mosaic", "Bridge");
  if (process.platform === "win32") {
    const local = process.env.LOCALAPPDATA ?? join(home, "AppData", "Local");
    return join(local, "Mosaic", "Bridge");
  }
  const xdg = process.env.XDG_RUNTIME_DIR;
  return xdg && existsSync(xdg) ? join(xdg, "mosaic-bridge") : join(home, ".mosaic", "bridge");
}

/** Find the running Editor's bridge. One Editor is the normal case; when several are
 *  running the registry lists them and we take the most recently started, which is the
 *  one a person just opened. */
function findDiscovery(explicit?: string): Discovery {
  const candidates: string[] = [];
  if (explicit) candidates.push(explicit);
  const base = sharedBase();
  const registry = join(base, "instance-registry.json");
  if (existsSync(registry)) {
    try {
      const reg = JSON.parse(readFileSync(registry, "utf8").replace(/^﻿/, ""));
      const entries: any[] = Array.isArray(reg) ? reg : reg.instances || reg.entries || [];
      const sorted = entries
        .filter((e) => e && (e.runtime_dir || e.project_hash))
        .sort((a, b) => (b.started_unix_seconds || 0) - (a.started_unix_seconds || 0));
      for (const e of sorted) {
        candidates.push(e.runtime_dir ? join(e.runtime_dir, "bridge-discovery.json") : join(base, e.project_hash, "bridge-discovery.json"));
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

/** The bridge authenticates every request with an HMAC over method, path and body —
 *  the same scheme the local MCP server uses, so nothing about the Editor's security
 *  posture changes by connecting through the cloud. */
async function callBridge(d: Discovery, route: string, params: unknown, timeoutMs: number): Promise<unknown> {
  const path = `/tools/${route}`;
  const bodyBuffer = Buffer.from(JSON.stringify({ parameters: params ?? {} }), "utf8");
  const nonce = randomUUID();
  const timestamp = String(Math.floor(Date.now() / 1000));
  const secret = Buffer.from(d.secret_base64, "base64");
  const payload = Buffer.concat([
    Buffer.from(`POST\n${path}\n${nonce}\n${timestamp}\n`, "utf8"),
    Buffer.from(createHmac("sha256", secret).update(bodyBuffer).digest("hex"), "utf8"),
  ]);
  const signature = createHmac("sha256", secret).update(payload).digest("base64");

  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), timeoutMs);
  try {
    const res = await fetch(`http://127.0.0.1:${d.port}${path}`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "X-Mosaic-Nonce": nonce,
        "X-Mosaic-Timestamp": timestamp,
        "X-Mosaic-Signature": signature,
      },
      body: bodyBuffer,
      signal: controller.signal,
    });
    const text = await res.text();
    try {
      return JSON.parse(text);
    } catch {
      return text;
    }
  } finally {
    clearTimeout(timer);
  }
}

function connect(args: Args, attempt = 0): void {
  const target = `${args.url}${args.url.includes("?") ? "&" : "?"}token=${encodeURIComponent(args.token)}`;
  const ws = new WebSocket(target);

  ws.on("open", () => {
    attempt = 0;
    let d: Discovery | null = null;
    try {
      d = findDiscovery(args.discoveryFile);
    } catch (e) {
      process.stdout.write(`connected to the service, but ${(e as Error).message}\n`);
    }
    process.stdout.write(
      `connector ready${d ? ` (Unity ${d.unity_version ?? "?"} on port ${d.port})` : ""}\n`
    );
  });

  ws.on("message", async (raw) => {
    let msg: any;
    try {
      msg = JSON.parse(raw.toString());
    } catch {
      return;
    }
    if (msg.type === "hello") {
      if (args.verbose) process.stdout.write(`authenticated as ${msg.user}\n`);
      return;
    }
    if (!msg.id || !msg.route) return;
    if (args.verbose) process.stdout.write(`-> ${msg.route}\n`);
    try {
      const d = findDiscovery(args.discoveryFile);
      const result = await callBridge(d, msg.route, msg.params, 120_000);
      ws.send(JSON.stringify({ id: msg.id, result }));
    } catch (err) {
      ws.send(JSON.stringify({ id: msg.id, error: (err as Error).message }));
    }
  });

  const retry = (why: string) => {
    const wait = Math.min(30_000, 1000 * 2 ** Math.min(attempt, 5));
    process.stdout.write(`${why}; reconnecting in ${Math.round(wait / 1000)}s\n`);
    setTimeout(() => connect(args, attempt + 1), wait);
  };
  ws.on("close", (code) => retry(`connection closed (${code})`));
  ws.on("error", (err) => {
    if (ws.readyState !== WebSocket.OPEN) retry(`connection error: ${err.message}`);
  });
}

connect(parseArgs(process.argv.slice(2)));
