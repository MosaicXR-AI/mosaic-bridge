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
import { setup, readConfig, writeConfig, addProject, statusReport, usage, servicePackages } from "./cli.js";
import { findDiscovery, bridgeAlive, type Discovery } from "./discovery.js";
import { createHash, createHmac, randomUUID } from "node:crypto";

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
  const stored = readConfig();
  const url = get("--url") || process.env.MOSAIC_CLOUD_URL || stored?.url || "";
  const token = get("--token") || process.env.MOSAIC_CLOUD_TOKEN || stored?.token || "";
  if (!url || !token) {
    process.stderr.write("not configured. Run: mosaic-connector setup\n");
    process.exit(2);
  }
  return { url, token, discoveryFile: get("--discovery-file"), verbose: argv.includes("--verbose") };
}

/** Subcommands run and exit; anything else falls through to the connection loop. */
async function main(argv: string[]): Promise<void> {
  const cmd = argv[0];
  if (cmd === "setup") {
    const get = (f: string) => (argv.indexOf(f) >= 0 ? argv[argv.indexOf(f) + 1] : undefined);
    await setup({ url: get("--url"), token: get("--token") });
    return;
  }
  if (cmd === "add") {
    const target = argv[1];
    if (!target) {
      process.stderr.write("usage: mosaic-connector add <path to Unity project>\n");
      process.exit(2);
    }
    // A project added later must get the same packages as one added during setup,
    // so ask the service again rather than adding Bridge alone.
    const stored = readConfig();
    const svc = stored ? await servicePackages(stored.url, stored.token) : null;
    // Record the project before touching it. A crash between the manifest write and
    // the config write leaves the project and the connector disagreeing about
    // reality, which is exactly what happened: `add` reported success, then died,
    // and `status` showed nothing.
    const cfg = readConfig();
    if (cfg && !cfg.projects.includes(target)) {
      cfg.projects.push(target);
      writeConfig(cfg);
    }
    const r = addProject(target, svc);
    process.stdout.write(r.message + "\n");
    if (r.added) {
      process.stdout.write("Open the project in Unity once so the packages import.\n");
    }
    return;
  }
  if (cmd === "status") {
    process.stdout.write(statusReport() + "\n");
    return;
  }
  if (cmd === "help" || cmd === "--help" || cmd === "-h") {
    process.stdout.write(usage() + "\n");
    return;
  }
  // No configuration yet and no command: walk the person through setup rather than
  // printing a usage error they cannot act on.
  if (!cmd && !readConfig()) {
    await setup();
    return;
  }
  connect(parseArgs(argv.filter((a) => a !== "run")));
}

/** The bridge authenticates every request with an HMAC over a canonical string.
 *
 *  This mirrors packages/mcp-server/src/hmac.ts exactly, including the v1
 *  length-prefixed framing: a signature that merely "looks right" is rejected, and
 *  the local MCP server is the reference implementation for what right means. */
function buildCanonical(nonce: string, timestamp: string, method: string, path: string, bodySha256: string): string {
  const len = (x: string) => Buffer.byteLength(x, "utf8");
  return [
    "v1",
    `${len(nonce)}:${nonce}`,
    `${len(timestamp)}:${timestamp}`,
    `${len(method)}:${method}`,
    `${len(path)}:${path}`,
    `${len(bodySha256)}:${bodySha256}`,
  ].join("\n");
}

function signRequest(secretBase64: string, method: string, path: string, body: Buffer) {
  const nonce = randomUUID().replace(/-/g, "");
  const timestamp = Math.floor(Date.now() / 1000).toString();
  const bodySha256 = createHash("sha256").update(body).digest("hex");
  const canonical = buildCanonical(nonce, timestamp, method.toUpperCase(), path, bodySha256);
  const signature = createHmac("sha256", Buffer.from(secretBase64, "base64"))
    .update(Buffer.from(canonical, "utf8"))
    .digest("hex");
  return { nonce, timestamp, signature };
}

async function bridgeRequest(
  d: Discovery,
  method: "GET" | "POST",
  path: string,
  body: unknown,
  timeoutMs: number
): Promise<unknown> {
  const bodyBuffer = body ? Buffer.from(JSON.stringify(body), "utf8") : Buffer.alloc(0);
  const { nonce, timestamp, signature } = signRequest(d.secret_base64, method, path, bodyBuffer);
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), timeoutMs);
  try {
    const res = await fetch(`http://127.0.0.1:${d.port}${path}`, {
      method,
      headers: {
        "Content-Type": "application/json",
        "X-Mosaic-Nonce": nonce,
        "X-Mosaic-Timestamp": timestamp,
        "X-Mosaic-Signature": signature,
      },
      body: bodyBuffer.length > 0 ? bodyBuffer : undefined,
      signal: controller.signal,
    });
    const text = await res.text();
    let parsed: unknown = text;
    try {
      parsed = JSON.parse(text);
    } catch {
      /* the bridge answered with plain text; pass it through */
    }
    if (!res.ok) throw new Error(`bridge ${res.status}: ${typeof parsed === "string" ? parsed : JSON.stringify(parsed)}`);
    return parsed;
  } finally {
    clearTimeout(timer);
  }
}

/** Routes the cloud understands: a bridge tool name executes, and two service routes
 *  let the cloud ask what this Editor is and what it can do. */
async function callBridge(d: Discovery, route: string, params: unknown, timeoutMs: number): Promise<unknown> {
  if (route === "_health") return bridgeRequest(d, "GET", "/health", undefined, timeoutMs);
  if (route === "_tools") return bridgeRequest(d, "GET", "/tools", undefined, timeoutMs);
  // The Editor registers tools as mosaic_<category>_<action>; the pipeline and its
  // docs speak of routes as <category>/<action>. Accept either spelling rather than
  // making the caller remember which layer it is talking to.
  const tool = route.includes("/") ? "mosaic_" + route.replace(/\//g, "_") : route;
  return bridgeRequest(d, "POST", "/execute", { tool, parameters: params ?? {} }, timeoutMs);
}

function connect(args: Args, attempt = 0): void {
  const target = `${args.url}${args.url.includes("?") ? "&" : "?"}token=${encodeURIComponent(args.token)}`;
  const ws = new WebSocket(target);

  ws.on("open", async () => {
    attempt = 0;
    let d: Discovery | null = null;
    try {
      const found = findDiscovery(args.discoveryFile);
      // Announce a version and a port only after something answers on them. The file
      // outlives the Editor, and an Editor whose bridge failed to compile leaves one
      // behind that describes a bridge which never ran.
      d = (await bridgeAlive(found)) ? found : null;
      if (!d) {
        process.stdout.write(
          "A Unity Editor was found on record, but its Mosaic Bridge is not answering.\n" +
            "  Usually the project is still importing, or the package failed to compile.\n" +
            "  Check the Unity Console for errors, then leave this running.\n"
        );
      }
    } catch {
      /* reported below as a waiting state, not as a failure */
    }
    if (d) {
      process.stdout.write(`connector ready (Unity ${d.unity_version ?? "?"} on port ${d.port})\n`);
    } else {
      // Do not say "ready" when there is no Editor: the previous version printed the
      // problem and "connector ready" one line apart, and a person reasonably read
      // the second line and stopped. It also blamed a closed project when the real
      // cause is usually a project without the Mosaic Bridge package.
      process.stdout.write(
        "connected to the service, waiting for a Unity Editor.\n" +
          "  Open a Unity project that has the Mosaic Bridge package installed.\n" +
          "  If it is already open, that project may not have the package: run\n" +
          "  mosaic-connector add <project path>, then reopen it in Unity.\n"
      );
      // Keep looking, so the state resolves itself when the Editor appears rather
      // than requiring the person to restart something.
      const poll = setInterval(async () => {
        try {
          const found = findDiscovery(args.discoveryFile);
          if (!(await bridgeAlive(found))) return; // recorded, but not answering yet
          clearInterval(poll);
          process.stdout.write(`connector ready (Unity ${found.unity_version ?? "?"} on port ${found.port})\n`);
        } catch {
          /* still waiting */
        }
      }, 3000);
      ws.on("close", () => clearInterval(poll));
    }
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
      // "fetch failed" is what Node says when nothing is listening, and it names
      // neither the cause nor a remedy. The person reading it is an instructor.
      const raw = (err as Error).message || String(err);
      const message = /fetch failed|ECONNREFUSED|ENOTFOUND/i.test(raw)
        ? "The Unity Editor is not answering. Its Mosaic Bridge is not running: the " +
          "project may still be importing, or the Mosaic package may have failed to " +
          "compile. Check the Unity Console for errors."
        : raw;
      ws.send(JSON.stringify({ id: msg.id, error: message }));
    }
  });

  const retry = (why: string, code?: number) => {
    // 4000 means the service accepted a newer connector for this user: another
    // process took the slot. Reconnecting would start a fight neither side wins,
    // so this one steps aside instead.
    if (code === 4000) {
      process.stdout.write("another connector took over this token; exiting\n");
      process.exit(0);
    }
    const wait = Math.min(30_000, 1000 * 2 ** Math.min(attempt, 5));
    process.stdout.write(`${why}; reconnecting in ${Math.round(wait / 1000)}s\n`);
    setTimeout(() => connect(args, attempt + 1), wait);
  };
  ws.on("close", (code) => retry(`connection closed (${code})`, code));
  ws.on("error", (err) => {
    if (ws.readyState !== WebSocket.OPEN) retry(`connection error: ${err.message}`);
  });
}

main(process.argv.slice(2));
